namespace HornViet.Application.Common;

/// <summary>
/// Model nhận diện ý định của chatbot: học từ câu mẫu đã gán nhãn, đoán ý định của câu khách gõ.
/// Interface ở tầng Application, cài đặt bằng ML.NET ở tầng Infrastructure (Services/MlNetIntentEngine) —
/// Application không phụ thuộc thư viện học máy cụ thể.
/// </summary>
/// <remarks>Đăng ký singleton: model đã nạp nằm trong bộ nhớ, dùng chung cho mọi request.</remarks>
public interface IIntentEngine
{
    /// <summary>Id của bản <c>ChatModel</c> đang nạp; null = chưa nạp model nào.</summary>
    int? LoadedModelId { get; }

    /// <summary>Nạp model đã huấn luyện (bytes lưu trong CSDL) để bắt đầu đoán.</summary>
    void Load(int modelId, byte[] model);

    /// <summary>Đoán ý định của câu đã chuẩn hóa (ChatText.Normalize); null khi chưa nạp model.</summary>
    IntentPrediction? Predict(string normalizedText);

    /// <summary>
    /// Huấn luyện: chia 80% câu mẫu để học, 20% để kiểm tra → đo độ chính xác; sau đó học lại trên toàn bộ dữ liệu
    /// để ra model dùng thật. Không thay model đang chạy — gọi <see cref="Load"/> sau khi lưu.
    /// </summary>
    IntentTrainingResult Train(IReadOnlyList<LabeledPhrase> phrases);
}

/// <summary>Một câu mẫu đã chuẩn hóa và nhãn (mã ý định).</summary>
public record LabeledPhrase(string Text, string Label);

/// <summary>Ý định đoán được và độ tin cậy 0–1 (xác suất của ý định đó).</summary>
public record IntentPrediction(string Label, double Confidence);

public record IntentTrainingResult(
    byte[] Model,
    double Accuracy,
    double MacroAccuracy,
    int TrainCount,
    int TestCount,
    IReadOnlyList<IntentMetric> PerIntent,
    IReadOnlyList<IntentConfusion> Confusions);

/// <summary>
/// Chỉ số của một ý định trên tập kiểm tra. Precision: trong các câu bot đoán là ý định này, bao nhiêu phần đúng;
/// Recall: trong các câu thật sự thuộc ý định này, bot nhận ra bao nhiêu phần; F1: trung bình điều hòa của hai số.
/// </summary>
public record IntentMetric(string Label, int Support, double Precision, double Recall, double F1);

/// <summary>Số câu ý định <paramref name="Actual"/> bị đoán nhầm thành <paramref name="Predicted"/> (từ ma trận nhầm lẫn).</summary>
public record IntentConfusion(string Actual, string Predicted, int Count);
