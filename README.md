# Zalo Native Client cho Windows 10 Mobile (UWP C#/XAML)

Dự án chat Zalo native dành riêng cho các thiết bị Windows 10 Mobile (Lumia 640, 650, 730, 950, 950XL...).

## 🚀 Cách Push lên GitHub & Tự động Build ARM

1. Mở PowerShell trong thư mục này và khởi tạo git:
   ```bash
   cd C:\Users\zakoo\.gemini\antigravity\scratch\ZaloW10M
   git init
   git add .
   git commit -m "Initial commit: ZaloW10M UWP native client"
   git remote add origin https://github.com/<your-username>/<your-repo-name>.git
   git branch -M main
   git push -u origin main
   ```

2. Vào tab **Actions** trên GitHub repository của bạn để theo dõi quá trình build ARM tự động.
3. Sau khi build xong, tải toàn bộ Artifacts về. Workflow tạo một certificate self-signed `CN=ZaloW10M`, ký gói trong lúc build và đính kèm `ZaloW10M_Test.cer`. Trước khi deploy qua Windows Device Portal, cài file `.cer` đó vào máy (chọn **Trusted People** / **Trusted Root**) rồi cài `.appxbundle`; nếu thiếu bước này Device Portal sẽ từ chối gói. Certificate này chỉ để thử nghiệm — thay bằng certificate của bạn khi phát hành.