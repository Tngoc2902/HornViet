// File thuộc SharedKernel: lớp cơ sở cho Value Object (Đối tượng giá trị) theo DDD.
// Khác với Entity (nhận diện bằng Id), Value Object không có định danh: hai đối tượng có cùng giá trị các thành
// phần thì được coi là một. VD: hai khoản tiền "100 VND" là như nhau dù là hai đối tượng khác nhau trong bộ nhớ.
// Value Object thường bất biến (immutable) — muốn đổi giá trị thì tạo đối tượng mới.
// Hiện tại chưa có lớp nào trong tầng Domain kế thừa ValueObject; lớp này được chuẩn bị sẵn và được kiểm thử
// trong ValueObjectTests (dùng lớp Money mẫu).
namespace Website.Domain.SharedKernel;

// IEquatable<ValueObject>: interface chuẩn của .NET, cam kết lớp có phương thức Equals(ValueObject?) so sánh
// bằng nhau theo kiểu mạnh (không cần ép kiểu từ object).
/// <summary>Đối tượng giá trị (Value Object) — so sánh bằng giá trị của các thành phần, không bằng định danh.</summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Trả về danh sách các thành phần tham gia so sánh bằng nhau (VD: số tiền và loại tiền tệ).
    /// Lớp con bắt buộc cài đặt (abstract) và phải trả về các thành phần theo cùng một thứ tự cố định.
    /// </summary>
    /// <returns>Chuỗi các giá trị thành phần dùng để so sánh và tính mã băm.</returns>
    // abstract trên phương thức: chỉ khai báo, không có thân; mỗi lớp con tự quyết định thành phần nào được so sánh.
    // IEnumerable<object>: một dãy giá trị có thể duyệt tuần tự (foreach).
    public abstract IEnumerable<object> GetEqualityComponents();

    /// <summary>
    /// So sánh bằng nhau theo giá trị: hai Value Object bằng nhau khi cùng kiểu và mọi thành phần bằng nhau.
    /// </summary>
    /// <param name="obj">Đối tượng cần so sánh (có thể null).</param>
    /// <returns>true nếu cùng kiểu và các thành phần bằng nhau theo đúng thứ tự; ngược lại false.</returns>
    // override: ghi đè phương thức Equals mặc định của object (mặc định chỉ so sánh tham chiếu/địa chỉ bộ nhớ).
    public override bool Equals(object? obj)
    {
        // Khác null và phải cùng kiểu thực tế lúc chạy (GetType): một Money không bao giờ bằng một
        // Value Object kiểu khác, kể cả khi các thành phần tình cờ trùng giá trị.
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        // Đã chắc chắn cùng kiểu nên ép kiểu an toàn về ValueObject để lấy danh sách thành phần.
        var valueObject = (ValueObject)obj;
        // LINQ SequenceEqual: so sánh hai dãy từng phần tử một theo thứ tự; bằng nhau khi cùng độ dài
        // và mọi cặp phần tử tương ứng đều Equals.
        return GetEqualityComponents()
                .SequenceEqual(valueObject.GetEqualityComponents());
    }

    /// <summary>
    /// Nạp chồng toán tử == để "a == b" so sánh theo giá trị thay vì so sánh tham chiếu.
    /// </summary>
    /// <param name="left">Vế trái.</param>
    /// <param name="right">Vế phải.</param>
    /// <returns>true nếu hai Value Object bằng nhau theo giá trị (hoặc cùng null).</returns>
    // static: phương thức thuộc về lớp, không thuộc về một đối tượng cụ thể; C# yêu cầu toán tử phải là static.
    public static bool operator ==(ValueObject left, ValueObject right)
    {
        // object.Equals(a, b) tĩnh: xử lý an toàn trường hợp null (cùng null → true, một bên null → false),
        // sau đó gọi Equals(object?) đã override ở trên để so sánh theo thành phần.
        return Equals(left, right);
    }

    /// <summary>Nạp chồng toán tử != — luôn là phủ định của ==; C# bắt buộc khai báo theo cặp với ==.</summary>
    /// <param name="left">Vế trái.</param>
    /// <param name="right">Vế phải.</param>
    /// <returns>true nếu hai Value Object khác nhau về giá trị.</returns>
    public static bool operator !=(ValueObject left, ValueObject right)
    {
        // Dùng lại cùng logic so sánh rồi đảo kết quả, đảm bảo == và != không bao giờ mâu thuẫn nhau.
        return !Equals(left, right);
    }

    /// <summary>
    /// Tính mã băm (hash code) từ các thành phần. Quy tắc của .NET: hai đối tượng Equals nhau thì phải có
    /// cùng mã băm, nếu không sẽ hoạt động sai khi làm khóa trong Dictionary hoặc phần tử của HashSet.
    /// </summary>
    /// <returns>Mã băm kết hợp (XOR) mã băm của mọi thành phần.</returns>
    public override int GetHashCode()
    {
        // LINQ Select: biến mỗi thành phần thành mã băm của nó.
        // x?.GetHashCode(): toán tử ?. — nếu x là null thì cả biểu thức trả về null thay vì gây lỗi.
        // ?? 0: toán tử ?? — nếu vế trái là null thì dùng giá trị 0 thay thế.
        // LINQ Aggregate: gộp dần các mã băm bằng phép XOR (^) thành một số duy nhất.
        // Lưu ý: Aggregate không có giá trị khởi tạo nên lớp con phải trả về ít nhất một thành phần,
        // nếu danh sách rỗng sẽ phát sinh InvalidOperationException.
        return GetEqualityComponents()
                .Select(x => x?.GetHashCode() ?? 0)
                .Aggregate((x, y) => x ^ y);
    }

    /// <summary>Cài đặt IEquatable&lt;ValueObject&gt;: so sánh theo giá trị với một ValueObject khác.</summary>
    /// <param name="other">Value Object cần so sánh (có thể null).</param>
    /// <returns>true nếu bằng nhau theo giá trị.</returns>
    public bool Equals(ValueObject? other)
    {
        // Chuyển về object? để gọi lại Equals(object?) ở trên — chỉ giữ một nơi chứa logic so sánh.
        return Equals((object?)other);
    }
}

// Lớp tiện ích cho Value Object chỉ gói một giá trị duy nhất (VD: số điện thoại, email) — lớp con chỉ cần
// kế thừa ValueObject<string> thay vì tự viết GetEqualityComponents.
/// <summary>
/// Value Object bao bọc đúng một giá trị kiểu <typeparamref name="T"/>; hai đối tượng bằng nhau khi Value bằng nhau.
/// </summary>
/// <typeparam name="T">Kiểu của giá trị được bao bọc.</typeparam>
public abstract class ValueObject<T> : ValueObject
{
    /// <summary>Giá trị được bao bọc.</summary>
    // protected set: chỉ lớp này và lớp con được gán Value; bên ngoài chỉ đọc — giúp Value Object bất biến.
    // default!: giá trị mặc định của T, dấu ! bỏ qua cảnh báo nullable vì lớp con sẽ gán giá trị thật.
    public T Value { get; protected set; } = default!;

    /// <summary>Thành phần so sánh duy nhất là Value.</summary>
    /// <returns>Dãy gồm một phần tử là Value.</returns>
    public override IEnumerable<object> GetEqualityComponents()
    {
        // yield return: trả từng phần tử của dãy một cách "lười" mà không cần tạo List; ở đây dãy chỉ có một
        // phần tử. Value! dùng toán tử ! để bỏ qua cảnh báo nullable khi chuyển T sang object.
        yield return Value!;
    }
}
