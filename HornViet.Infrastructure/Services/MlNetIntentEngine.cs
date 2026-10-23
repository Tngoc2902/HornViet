using HornViet.Application.Common;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;
using Microsoft.ML.Transforms.Text;

namespace HornViet.Infrastructure.Services;

/// <summary>
/// Model nhận diện ý định bằng ML.NET (thư viện học máy của Microsoft, chạy ngay trong tiến trình .NET, không cần GPU).
/// Quy trình (pipeline):
///   1) FeaturizeText: biến câu thành vector đặc trưng — túi từ đơn + cặp từ liền nhau (word 1–2-gram) và các cụm 1–5 ký tự
///      (char n-gram, giúp chịu được lỗi gõ / viết dính / thiếu chữ), chuẩn hóa L2;
///   2) LbfgsMaximumEntropy: hồi quy logistic đa lớp (softmax) tối ưu bằng L-BFGS, điều chuẩn L2 nhẹ — cho xác suất từng
///      ý định, xác suất lớn nhất là độ tin cậy.
/// Cấu hình chọn bằng kiểm tra chéo 5 phần trên bộ câu mẫu ban đầu (~840 câu, 21 ý định): micro accuracy ≈ 0,78,
/// cao hơn SDCA + char 3-gram (≈ 0,75) mà độ tin cậy của câu đoán sai không tăng.
/// Câu đầu vào đã qua ChatText.Normalize (chữ thường, bỏ dấu, đổi teencode).
/// </summary>
public sealed class MlNetIntentEngine : IIntentEngine
{
    private const string LabelKey = "LabelKey";
    private const double TestFraction = 0.2;
    private const int Seed = 1;

    private readonly object _sync = new();
    private PredictionEngine<PhraseRow, PhrasePrediction>? _predictor;

    public int? LoadedModelId { get; private set; }

    private sealed class PhraseRow
    {
        public string Text { get; set; } = "";
        public string Label { get; set; } = "";
    }

    private sealed class PhrasePrediction
    {
        public string PredictedLabel { get; set; } = "";
        public float[] Score { get; set; } = [];
    }

    public void Load(int modelId, byte[] model)
    {
        var ml = new MLContext(Seed);
        using var stream = new MemoryStream(model);
        var transformer = ml.Model.Load(stream, out _);
        var predictor = ml.Model.CreatePredictionEngine<PhraseRow, PhrasePrediction>(transformer);
        // PredictionEngine không an toàn đa luồng → mọi lần đoán đi qua khóa; đổi model cũng trong khóa.
        lock (_sync)
        {
            _predictor?.Dispose();
            _predictor = predictor;
            LoadedModelId = modelId;
        }
    }

    public IntentPrediction? Predict(string normalizedText)
    {
        lock (_sync)
        {
            if (_predictor is null)
                return null;
            var p = _predictor.Predict(new PhraseRow { Text = normalizedText });
            return new IntentPrediction(p.PredictedLabel, p.Score.Length == 0 ? 0 : p.Score.Max());
        }
    }

    public IntentTrainingResult Train(IReadOnlyList<LabeledPhrase> phrases)
    {
        var ml = new MLContext(Seed);
        var rows = phrases.Select(p => new PhraseRow { Text = p.Text, Label = p.Label }).ToList();
        var (train, test) = StratifiedSplit(rows);

        // Bước 1 — đánh giá: học trên 80%, đoán thử 20% còn lại (model chưa từng thấy) để đo độ chính xác.
        var evalModel = Pipeline(ml).Fit(ml.Data.LoadFromEnumerable(train));
        var predictions = evalModel.Transform(ml.Data.LoadFromEnumerable(test));
        var metrics = ml.MulticlassClassification.Evaluate(predictions, labelColumnName: LabelKey);

        VBuffer<ReadOnlyMemory<char>> keys = default;
        predictions.Schema[LabelKey].GetKeyValues(ref keys);
        var labels = keys.DenseValues().Select(k => k.ToString()).ToArray();
        var matrix = metrics.ConfusionMatrix;

        var perIntent = new List<IntentMetric>();
        var confusions = new List<IntentConfusion>();
        for (var i = 0; i < labels.Length && i < matrix.NumberOfClasses; i++)
        {
            var support = (int)Math.Round(matrix.Counts[i].Sum());
            var precision = matrix.PerClassPrecision[i];
            var recall = matrix.PerClassRecall[i];
            var f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
            perIntent.Add(new IntentMetric(labels[i], support, Round(precision), Round(recall), Round(f1)));
            for (var j = 0; j < matrix.NumberOfClasses; j++)
                if (i != j && matrix.Counts[i][j] > 0)
                    confusions.Add(new IntentConfusion(labels[i], labels[j], (int)Math.Round(matrix.Counts[i][j])));
        }

        // Bước 2 — model dùng thật: học lại trên toàn bộ câu mẫu (không phí 20% dữ liệu).
        var all = ml.Data.LoadFromEnumerable(rows);
        var finalModel = Pipeline(ml).Fit(all);
        using var stream = new MemoryStream();
        ml.Model.Save(finalModel, all.Schema, stream);

        return new IntentTrainingResult(stream.ToArray(), Round(metrics.MicroAccuracy), Round(metrics.MacroAccuracy),
            train.Count, test.Count, perIntent, confusions.OrderByDescending(c => c.Count).Take(15).ToList());
    }

    private static IEstimator<ITransformer> Pipeline(MLContext ml) =>
        ml.Transforms.Conversion.MapValueToKey(LabelKey, nameof(PhraseRow.Label),
                keyOrdinality: ValueToKeyMappingEstimator.KeyOrdinality.ByValue)
            .Append(ml.Transforms.Text.FeaturizeText("Features", new TextFeaturizingEstimator.Options
            {
                CaseMode = TextNormalizingEstimator.CaseMode.None, // đã chữ thường, bỏ dấu ở ChatText.Normalize
                KeepPunctuations = false,
                KeepNumbers = true,
                WordFeatureExtractor = new WordBagEstimator.Options { NgramLength = 2, UseAllLengths = true },
                CharFeatureExtractor = new WordBagEstimator.Options { NgramLength = 5, UseAllLengths = true },
                Norm = TextFeaturizingEstimator.NormFunction.L2
            }, nameof(PhraseRow.Text)))
            .Append(ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy(new LbfgsMaximumEntropyMulticlassTrainer.Options
            {
                LabelColumnName = LabelKey,
                FeatureColumnName = "Features",
                L2Regularization = 0.01f,
                L1Regularization = 0,
                NumberOfThreads = 1 // kết quả lặp lại được giữa các lần huấn luyện; máy chủ miễn phí chỉ có ít CPU
            }))
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

    /// <summary>Chia 80/20 theo từng ý định (phân tầng) để ý định nào cũng có câu trong tập kiểm tra.</summary>
    private static (List<PhraseRow> Train, List<PhraseRow> Test) StratifiedSplit(List<PhraseRow> rows)
    {
        var random = new Random(Seed);
        var train = new List<PhraseRow>();
        var test = new List<PhraseRow>();
        foreach (var group in rows.GroupBy(r => r.Label))
        {
            var shuffled = group.OrderBy(_ => random.Next()).ToList();
            var testCount = shuffled.Count >= 5 ? Math.Max(1, (int)Math.Round(shuffled.Count * TestFraction)) : 0;
            test.AddRange(shuffled.Take(testCount));
            train.AddRange(shuffled.Skip(testCount));
        }
        return (train, test);
    }

    private static double Round(double value) => Math.Round(value, 4);
}
