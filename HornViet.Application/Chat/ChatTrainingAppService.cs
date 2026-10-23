using System.Text.RegularExpressions;
using HornViet.Application.Common;
using HornViet.Domain;
using HornViet.Domain.Chat.Entities;
using HornViet.Domain.Chat.Enums;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Chat;

/// <summary>
/// Trang quản trị "Huấn luyện chatbot": quản lý ý định và câu mẫu, bấm huấn luyện để ra model mới, xem độ chính xác,
/// gán nhãn các câu bot chưa hiểu để lần huấn luyện sau bot hiểu được.
/// </summary>
public interface IChatTrainingAppService
{
    Task<ListResultDto<ChatIntentDto>> GetIntentsAsync();
    Task<ChatIntentDto> GetIntentAsync(int id);
    Task<ChatIntentDto> CreateIntentAsync(CreateOrUpdateChatIntentInput input);
    Task<ChatIntentDto> UpdateIntentAsync(CreateOrUpdateChatIntentInput input);
    Task DeleteIntentAsync(int id);

    /// <summary>Huấn luyện từ câu mẫu hiện tại; bot dùng model mới ngay.</summary>
    Task<ChatModelDto> TrainAsync();

    /// <summary>Model đang dùng (null = chưa huấn luyện lần nào).</summary>
    Task<ChatModelDto?> GetModelAsync();

    /// <summary>Thử một câu: bot đoán ý định gì, độ tin cậy bao nhiêu và sẽ trả lời ra sao (không lưu lại).</summary>
    Task<ChatTestResultDto> TestAsync(TestChatInput input);

    /// <summary>Câu của khách mà bot chưa hiểu (độ tin cậy dưới ngưỡng), mới nhất trước.</summary>
    Task<PagedResultDto<ChatUnrecognizedDto>> GetUnrecognizedAsync(PagedInput input);

    /// <summary>Gán câu chưa hiểu vào một ý định: thêm thành câu mẫu (đã che thông tin cá nhân).</summary>
    Task LabelAsync(LabelChatMessageInput input);

    /// <summary>Bỏ qua câu chưa hiểu (câu vô nghĩa, không liên quan).</summary>
    Task DismissAsync(long messageId);
}

public partial class ChatTrainingAppService : IChatTrainingAppService
{
    private const string NotFound = "Không tìm thấy ý định.";

    private readonly IRepository<ChatIntent, int> _intentRepository;
    private readonly IRepository<ChatPhrase, int> _phraseRepository;
    private readonly IRepository<ChatModel, int> _modelRepository;
    private readonly IRepository<ChatMessage, long> _messageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSession _session;
    private readonly ChatModelManager _models;
    private readonly IIntentEngine _engine;
    private readonly ChatBot _bot;

    public ChatTrainingAppService(IRepository<ChatIntent, int> intentRepository, IRepository<ChatPhrase, int> phraseRepository,
        IRepository<ChatModel, int> modelRepository, IRepository<ChatMessage, long> messageRepository, IUnitOfWork unitOfWork,
        IAppSession session, ChatModelManager models, IIntentEngine engine, ChatBot bot)
    {
        _intentRepository = intentRepository;
        _phraseRepository = phraseRepository;
        _modelRepository = modelRepository;
        _messageRepository = messageRepository;
        _unitOfWork = unitOfWork;
        _session = session;
        _models = models;
        _engine = engine;
        _bot = bot;
    }

    // ------------------------------------------------------------------ Ý định & câu mẫu

    public async Task<ListResultDto<ChatIntentDto>> GetIntentsAsync()
    {
        var items = await _intentRepository.GetAll().AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new ChatIntentDto
            {
                Id = i.Id,
                Code = i.Code,
                Name = i.Name,
                Answer = i.Answer,
                LinkLabel = i.LinkLabel,
                LinkUrl = i.LinkUrl,
                Action = i.Action,
                IsActive = i.IsActive,
                PhraseCount = i.Phrases.Count
            })
            .ToListAsync();
        return new ListResultDto<ChatIntentDto>(items);
    }

    public async Task<ChatIntentDto> GetIntentAsync(int id)
    {
        var intent = await _intentRepository.GetAll().AsNoTracking().Include(i => i.Phrases)
                         .FirstOrDefaultAsync(i => i.Id == id) ?? throw new EntityNotFoundException(NotFound);
        return ToDto(intent);
    }

    public async Task<ChatIntentDto> CreateIntentAsync(CreateOrUpdateChatIntentInput input)
    {
        var intent = new ChatIntent();
        await ApplyAsync(intent, input);
        await _intentRepository.InsertAsync(intent);
        await _unitOfWork.SaveChangesAsync();
        return await GetIntentAsync(intent.Id);
    }

    public async Task<ChatIntentDto> UpdateIntentAsync(CreateOrUpdateChatIntentInput input)
    {
        if (input.Id is not { } id) throw new UserFriendlyException("Thiếu mã ý định.");
        var intent = await _intentRepository.GetAll().Include(i => i.Phrases).FirstOrDefaultAsync(i => i.Id == id)
                     ?? throw new EntityNotFoundException(NotFound);
        await ApplyAsync(intent, input);
        await _unitOfWork.SaveChangesAsync();
        return await GetIntentAsync(intent.Id);
    }

    public async Task DeleteIntentAsync(int id)
    {
        var intent = await _intentRepository.GetAll().FirstOrDefaultAsync(i => i.Id == id)
                     ?? throw new EntityNotFoundException(NotFound);
        _intentRepository.Delete(intent); // câu mẫu xóa dây chuyền; tin nhắn cũ chỉ lưu mã ý định nên không ảnh hưởng
        await _unitOfWork.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Huấn luyện

    public async Task<ChatModelDto> TrainAsync()
    {
        await _models.TrainAsync(_session.UserId);
        return (await GetModelAsync())!;
    }

    public async Task<ChatModelDto?> GetModelAsync()
    {
        var model = await _modelRepository.GetAll().AsNoTracking().OrderByDescending(m => m.Id)
            .Select(m => new { m.Id, m.CreationTime, m.IntentCount, m.PhraseCount, m.Accuracy, m.MacroAccuracy, m.Report, m.DataHash })
            .FirstOrDefaultAsync();
        if (model is null)
            return null;

        var report = ChatModelManager.ReadReport(model.Report);
        var names = await _intentRepository.GetAll().AsNoTracking().ToDictionaryAsync(i => i.Code, i => i.Name);
        string Name(string code) => names.GetValueOrDefault(code, code);
        return new ChatModelDto
        {
            Id = model.Id,
            CreationTime = model.CreationTime,
            IntentCount = model.IntentCount,
            PhraseCount = model.PhraseCount,
            Accuracy = model.Accuracy,
            MacroAccuracy = model.MacroAccuracy,
            TrainCount = report.TrainCount,
            TestCount = report.TestCount,
            PerIntent = report.PerIntent
                .Select(m => new IntentMetricDto { Code = m.Label, Name = Name(m.Label), Support = m.Support, Precision = m.Precision, Recall = m.Recall, F1 = m.F1 })
                .OrderBy(m => m.F1).ToList(),
            Confusions = report.Confusions
                .Select(c => new IntentConfusionDto { Actual = Name(c.Actual), Predicted = Name(c.Predicted), Count = c.Count }).ToList(),
            Warnings = report.Warnings,
            IsOutdated = model.DataHash != await _models.ComputeDataHashAsync(),
            IsLoaded = _engine.LoadedModelId == model.Id
        };
    }

    public async Task<ChatTestResultDto> TestAsync(TestChatInput input)
    {
        var reply = await _bot.ReplyAsync(input.Text, _session.UserId, null);
        var code = reply.Prediction?.Label;
        var name = code is null ? null : await _intentRepository.GetAll().Where(i => i.Code == code).Select(i => i.Name).FirstOrDefaultAsync();
        return new ChatTestResultDto
        {
            Normalized = ChatText.Normalize(input.Text),
            IntentCode = code,
            IntentName = name,
            Confidence = reply.Prediction?.Confidence,
            BelowThreshold = !reply.Understood,
            Threshold = AppConsts.ChatConfidenceThreshold,
            Reply = new ChatMessageDto
            {
                Sender = ChatSender.Bot,
                Content = reply.Content,
                CreationTime = DateTime.Now,
                Products = reply.Payload.Products,
                Orders = reply.Payload.Orders,
                Links = reply.Payload.Links,
                QuickReplies = reply.Payload.QuickReplies,
                SuggestStaff = reply.Payload.SuggestStaff,
                Ai = reply.Payload.Ai
            }
        };
    }

    // ------------------------------------------------------------------ Câu bot chưa hiểu

    public async Task<PagedResultDto<ChatUnrecognizedDto>> GetUnrecognizedAsync(PagedInput input)
    {
        var query = _messageRepository.GetAll().AsNoTracking().Where(m => m.NeedsReview && m.Sender == ChatSender.Customer);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(m => m.Id).PageBy(input)
            .Select(m => new ChatUnrecognizedDto
            {
                MessageId = m.Id,
                Text = m.Content,
                IntentCode = m.IntentCode,
                Confidence = m.Confidence,
                CreationTime = m.CreationTime
            })
            .ToListAsync();
        items.ForEach(i => i.Text = ChatText.MaskPersonalInfo(i.Text));
        return new PagedResultDto<ChatUnrecognizedDto>(total, items);
    }

    public async Task LabelAsync(LabelChatMessageInput input)
    {
        var message = await FindReviewAsync(input.MessageId);
        var intent = await _intentRepository.GetAll().Include(i => i.Phrases).FirstOrDefaultAsync(i => i.Id == input.IntentId)
                     ?? throw new EntityNotFoundException(NotFound);
        var text = CleanPhrase(ChatText.MaskPersonalInfo(message.Content));
        var normalized = ChatText.Normalize(text);
        if (normalized.Length > 0 && intent.Phrases.All(p => ChatText.Normalize(p.Text) != normalized))
            intent.Phrases.Add(new ChatPhrase { Text = text });
        message.NeedsReview = false;
        message.IntentCode = intent.Code;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DismissAsync(long messageId)
    {
        var message = await FindReviewAsync(messageId);
        message.NeedsReview = false;
        await _unitOfWork.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ Hỗ trợ

    private async Task<ChatMessage> FindReviewAsync(long id) =>
        await _messageRepository.GetAll().FirstOrDefaultAsync(m => m.Id == id && m.Sender == ChatSender.Customer)
        ?? throw new EntityNotFoundException("Không tìm thấy câu hỏi.");

    private async Task ApplyAsync(ChatIntent intent, CreateOrUpdateChatIntentInput input)
    {
        var name = input.Name.Trim();
        var code = ToCode(string.IsNullOrWhiteSpace(input.Code) ? name : input.Code);
        if (code.Length == 0)
            throw new UserFriendlyException("Mã ý định không hợp lệ.");
        if (await _intentRepository.GetAll().AnyAsync(i => i.Code == code && i.Id != intent.Id))
            throw new UserFriendlyException($"Mã ý định \"{code}\" đã được dùng.");
        if (!string.IsNullOrWhiteSpace(input.LinkUrl) && !input.LinkUrl.Trim().StartsWith('/'))
            throw new UserFriendlyException("Đường dẫn của nút phải là trang trong cửa hàng, bắt đầu bằng \"/\" (VD /ho-tro#doi-tra).");

        intent.Code = code;
        intent.Name = name;
        intent.Answer = input.Answer.Trim();
        intent.LinkLabel = string.IsNullOrWhiteSpace(input.LinkLabel) ? null : input.LinkLabel.Trim();
        intent.LinkUrl = string.IsNullOrWhiteSpace(input.LinkUrl) ? null : input.LinkUrl.Trim();
        intent.Action = Enum.IsDefined(input.Action) ? input.Action : ChatIntentAction.None;
        intent.IsActive = input.IsActive;

        // Câu mẫu: bỏ dòng trống, câu quá dài và câu trùng nhau sau khi chuẩn hóa ("Lược sừng" = "luoc sung").
        var wanted = new List<string>();
        var seen = new HashSet<string>();
        foreach (var raw in input.Phrases)
        {
            var text = CleanPhrase(raw);
            var normalized = ChatText.Normalize(text);
            if (normalized.Length > 0 && seen.Add(normalized))
                wanted.Add(text);
        }
        foreach (var removed in intent.Phrases.Where(p => !wanted.Contains(p.Text)).ToList())
        {
            intent.Phrases.Remove(removed);
            _phraseRepository.Delete(removed);
        }
        foreach (var text in wanted.Where(t => intent.Phrases.All(p => p.Text != t)))
            intent.Phrases.Add(new ChatPhrase { Text = text });
    }

    private static string CleanPhrase(string text)
    {
        var t = WhitespaceRegex().Replace(text, " ").Trim();
        return t.Length > 300 ? t[..300] : t;
    }

    /// <summary>"Hỏi giá" → "hoi_gia".</summary>
    private static string ToCode(string text)
    {
        var code = string.Join('_', ChatText.Fold(text).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return code.Length > 64 ? code[..64].TrimEnd('_') : code;
    }

    private static ChatIntentDto ToDto(ChatIntent i) => new()
    {
        Id = i.Id,
        Code = i.Code,
        Name = i.Name,
        Answer = i.Answer,
        LinkLabel = i.LinkLabel,
        LinkUrl = i.LinkUrl,
        Action = i.Action,
        IsActive = i.IsActive,
        PhraseCount = i.Phrases.Count,
        Phrases = i.Phrases.OrderBy(p => p.Id).Select(p => p.Text).ToList()
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
