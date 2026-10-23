using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HornViet.Application.Common;
using HornViet.Domain;
using HornViet.Domain.Chat.Entities;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Chat;

/// <summary>Báo cáo huấn luyện lưu ở ChatModel.Report (JSON).</summary>
public class ChatModelReport
{
    public int TrainCount { get; set; }
    public int TestCount { get; set; }
    public List<IntentMetric> PerIntent { get; set; } = new();
    public List<IntentConfusion> Confusions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Huấn luyện và nạp model của chatbot. Model mới nhất lưu trong bảng ChatModels; tiến trình khởi động lại (deploy,
/// máy chủ ngủ dậy) thì nạp lại từ CSDL ở tin nhắn đầu tiên. CSDL mới chưa có model thì tự huấn luyện từ câu mẫu.
/// </summary>
public class ChatModelManager
{
    /// <summary>Giữ lại vài bản gần nhất để đối chiếu; bản cũ hơn bị xóa (mỗi bản cỡ vài trăm KB).</summary>
    private const int KeepModels = 5;

    // Một tiến trình chỉ nạp / huấn luyện một lần tại một thời điểm (nhiều khách nhắn cùng lúc sau khi deploy).
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IIntentEngine _engine;
    private readonly IRepository<ChatModel, int> _modelRepository;
    private readonly IRepository<ChatIntent, int> _intentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChatModelManager(IIntentEngine engine, IRepository<ChatModel, int> modelRepository,
        IRepository<ChatIntent, int> intentRepository, IUnitOfWork unitOfWork)
    {
        _engine = engine;
        _modelRepository = modelRepository;
        _intentRepository = intentRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Bảo đảm bot có model để dùng: nạp bản mới nhất, hoặc huấn luyện lần đầu nếu chưa có bản nào.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (_engine.LoadedModelId is not null)
            return;
        await Gate.WaitAsync();
        try
        {
            if (_engine.LoadedModelId is not null)
                return;
            var latest = await _modelRepository.GetAll().AsNoTracking().OrderByDescending(m => m.Id)
                .Select(m => new { m.Id, m.Data }).FirstOrDefaultAsync();
            if (latest is not null)
                _engine.Load(latest.Id, latest.Data);
            else if (await _intentRepository.GetAll().AnyAsync(i => i.IsActive && i.Phrases.Any()))
                await TrainCoreAsync(null);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Huấn luyện từ câu mẫu hiện tại, lưu model mới và cho bot dùng ngay.</summary>
    public async Task<ChatModel> TrainAsync(long? creatorUserId)
    {
        await Gate.WaitAsync();
        try
        {
            return await TrainCoreAsync(creatorUserId);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Dấu vân tay của bộ câu mẫu hiện tại (các ý định đang bật).</summary>
    public async Task<string> ComputeDataHashAsync() => Hash(await LoadDataAsync());

    public static ChatModelReport ReadReport(string json) =>
        JsonSerializer.Deserialize<ChatModelReport>(json) ?? new ChatModelReport();

    private async Task<ChatModel> TrainCoreAsync(long? creatorUserId)
    {
        var data = await LoadDataAsync();
        if (data.Count < 2)
            throw new UserFriendlyException("Cần ít nhất 2 ý định đang bật để huấn luyện.");
        var thin = data.Where(d => d.Phrases.Count < AppConsts.ChatMinPhrasesPerIntent).Select(d => $"“{d.Name}” ({d.Phrases.Count})").ToList();
        if (thin.Count > 0)
            throw new UserFriendlyException(
                $"Mỗi ý định cần ít nhất {AppConsts.ChatMinPhrasesPerIntent} câu mẫu khác nhau. Còn thiếu: {string.Join(", ", thin)}.");

        // Một câu nằm ở hai ý định làm model "học" mâu thuẫn — vẫn huấn luyện nhưng báo cho quản trị biết để sửa.
        var warnings = data.SelectMany(d => d.Phrases.Select(p => (Phrase: p, d.Name)))
            .GroupBy(x => x.Phrase)
            .Where(g => g.Count() > 1)
            .Select(g => $"Câu “{g.Key}” có ở nhiều ý định: {string.Join(", ", g.Select(x => x.Name))}.")
            .Take(20)
            .ToList();

        var phrases = data.SelectMany(d => d.Phrases.Select(p => new LabeledPhrase(p, d.Code))).ToList();
        var result = _engine.Train(phrases);

        var report = new ChatModelReport
        {
            TrainCount = result.TrainCount,
            TestCount = result.TestCount,
            PerIntent = result.PerIntent.ToList(),
            Confusions = result.Confusions.ToList(),
            Warnings = warnings
        };
        var model = new ChatModel
        {
            Data = result.Model,
            DataHash = Hash(data),
            IntentCount = data.Count,
            PhraseCount = phrases.Count,
            Accuracy = result.Accuracy,
            MacroAccuracy = result.MacroAccuracy,
            Report = JsonSerializer.Serialize(report),
            CreatorUserId = creatorUserId
        };
        await _modelRepository.InsertAsync(model);
        await _unitOfWork.SaveChangesAsync();
        _engine.Load(model.Id, model.Data);

        var keepFrom = await _modelRepository.GetAll().OrderByDescending(m => m.Id).Skip(KeepModels - 1)
            .Select(m => (int?)m.Id).FirstOrDefaultAsync();
        if (keepFrom is { } cutoff)
            await _modelRepository.GetAll().Where(m => m.Id < cutoff).ExecuteDeleteAsync();
        return model;
    }

    private record IntentData(string Code, string Name, List<string> Phrases);

    /// <summary>Câu mẫu của các ý định đang bật, đã chuẩn hóa và bỏ trùng trong từng ý định.</summary>
    private async Task<List<IntentData>> LoadDataAsync()
    {
        var intents = await _intentRepository.GetAll().AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Code)
            .Select(i => new { i.Code, i.Name, Phrases = i.Phrases.Select(p => p.Text).ToList() })
            .ToListAsync();
        return intents
            .Select(i => new IntentData(i.Code, i.Name,
                i.Phrases.Select(ChatText.Normalize).Where(p => p.Length > 0).Distinct().ToList()))
            .ToList();
    }

    private static string Hash(IEnumerable<IntentData> data)
    {
        var lines = data.SelectMany(d => d.Phrases.Select(p => $"{d.Code}\t{p}")).Order(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)))).ToLowerInvariant();
    }
}
