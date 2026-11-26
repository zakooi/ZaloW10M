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
3. Sau khi build xong, tải file `.appx` từ Artifacts về. Workflow build với `AppxPackageSigningEnabled=false`, nên để deploy qua Windows Device Portal bạn cần bật **Developer Mode** trên máy và cài certificate dùng để ký lại gói (hoặc tự ký gói trước khi deploy). Nếu máy không ở Developer Mode, Device Portal sẽ từ chối cài `.appx` unsigned.