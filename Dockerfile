# Ảnh Docker của HornViet.Api — dùng để deploy lên Render (render.yaml) hoặc bất kỳ nền tảng chạy container nào.
# Build:  docker build -t hornviet-api .
# Chạy:   docker run -p 8080:8080 -e PORT=8080 -e ConnectionStrings__Default="..." -e Jwt__SecurityKey="..." hornviet-api

# ---------- Giai đoạn 1: build bằng .NET SDK ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Chép file dự án trước rồi restore: lớp cache này chỉ chạy lại khi thêm / đổi gói NuGet.
COPY SharedKernel/SharedKernel.csproj SharedKernel/
COPY HornViet.Domain/HornViet.Domain.csproj HornViet.Domain/
COPY HornViet.Application/HornViet.Application.csproj HornViet.Application/
COPY HornViet.Infrastructure/HornViet.Infrastructure.csproj HornViet.Infrastructure/
COPY HornViet.Api/HornViet.Api.csproj HornViet.Api/
RUN dotnet restore HornViet.Api/HornViet.Api.csproj

COPY . .
RUN dotnet publish HornViet.Api/HornViet.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Giai đoạn 2: chỉ runtime ASP.NET (nhẹ, không có SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Giờ Việt Nam cho DateTime.Now (ngày tạo đơn, thống kê theo ngày); container mặc định là UTC.
ENV TZ=Asia/Ho_Chi_Minh \
    ASPNETCORE_ENVIRONMENT=Production \
    PORT=8080
EXPOSE 8080

COPY --from=build /app/publish .
# Thư mục ghi được cho user không phải root: ảnh tải lên khi chưa cấu hình Cloudinary, email .eml khi chưa cấu hình SMTP.
RUN mkdir -p /app/wwwroot/uploads /app/App_Data \
    && chown -R $APP_UID:$APP_UID /app/wwwroot/uploads /app/App_Data
USER $APP_UID

ENTRYPOINT ["dotnet", "HornViet.Api.dll"]
