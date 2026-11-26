# zalo_core — Rust core cho Zalo-compatible protocol/crypto

Crate Rust thuần túy, **clean-room**: các thuật toán được mô tả lại từ hành vi đã có trong mã C# của repo `ZaloW10M` (`Core/ZaloCrypto.cs`, `Core/ZaloProtocol.cs`) và từ các primitive chuẩn công khai. **Không copy code hay tài sản từ APK.** APK đã được cung cấp và phân tích; chi tiết tại [`docs/apk-findings.md`](docs/apk-findings.md).

## Trạng thái

- [x] Định nghĩa frame: `version`, `cmd`, `sub_cmd`, payload, các chế độ `encrypt` 0/1/2/3.
- [x] `generate_sign_key`: sort theo key, nối value, prefix + MD5 hex.
- [x] AES-CBC/PKCS7 với IV = 0 (accept key 16/24/32 byte).
- [x] AES-GCM theo layout `IV(16) || tag(16) || ciphertext`.
- [x] C ABI (`zalo_*`) để P/Invoke, kèm hàm giải phóng bộ nhớ.
- [x] 6 unit test, đối chiếu với vector độc lập từ OpenSSL và Python `cryptography`.
- [x] Smoke test gọi C ABI từ C: `signkey=6c5d80bd07783d9b32e1a39690d1d451`, ping frame `010200017b226576656e744964223a3132337d`.
- [x] Phân tích XAPK bằng REA: inventory, chữ ký v2/v3, manifest, 46,897 class, ánh xạ JNI → `libznetwork.so` / `libzalosqlite.so` / `libnative_utils.so` / `libzvision-jni.so`.
- [x] `src/yuv.rs`: I420↔NV21 + rotate 90/180/270, đối chiếu vector tham chiếu độc lập (10/10 test).
- [ ] Đối chiếu byte-for-byte hàm sign với `libnative_utils.so` trên thiết bị/thư viện ARM.
- [ ] Scale I420 (JNI `nativeI420Scale`) và I420→ARGB/ABGR.
- [ ] Build ARM cho UWP / đóng gói APPX / deploy W10M (cần máy Windows).

## Build và test

```bash
source "$HOME/.cargo/env"
cd /workspaces/ZaloW10M/zalo-rust-core
cargo test
cargo build --release
```

Artifacts:

- `target/release/libzalo_core.a` — static library (dùng cho UWP native linkage).
- `target/release/libzalo_core.so` — cdylib (desktop Linux/macOS/Windows tương ứng `.dll`).
- `target/release/libzalo_core.rlib` — Rust library để dùng từ crate khác.

Smoke test C ABI:

```bash
gcc ffi_smoke.c target/release/libzalo_core.a -ldl -lpthread -lm -o /tmp/zalo_ffi_smoke
/tmp/zalo_ffi_smoke
```

## Tích hợp vào UWP C# (Windows 10 Mobile)

1. Copy `uwp/ZaloCoreNative.cs` vào project C#, ví dụ `Core/ZaloCoreNative.cs`, và thêm vào `<Compile Include=...>` trong `ZaloW10M.csproj`.
2. Build library Rust cho đúng kiến trúc mục tiêu (`ARM`/`ARM64`/`x86`/`x64`), đổi tên thành `zalo_core.lib`/`zalo_core.dll` rồi đưa vào package APPX.
3. Giữ nguyên tên `zalo_core` trong `DllImport`; wrapper đã marshal chuỗi dưới dạng UTF-8 + NUL để tránh lỗi code page trên Windows.
4. Thay dần các gọi `ZaloCrypto`/`ZaloProtocol` bằng `ZaloCoreNative`, dùng test đối chiếu để đảm bảo cùng behavior.

**Lưu ý quan trọng:** Windows 10 Mobile là UWP ARM32 sandbox. Không cài được file `.exe` Rust tùy ý. `rustc --print target-list` có liệt kê `thumbv7a-uwp-windows-msvc`, nhưng `rustup target add` báo **không có prebuilt artifacts** (tier 3). Muốn ARM32 native thường cần `nightly` + `-Z build-std` + custom target JSON, MSVC linker, Windows SDK 10.0.16299 và máy Windows có Visual Studio. Phần này **chưa được kiểm chứng** trong môi trường Linux này — mình mới chỉ chuẩn bị source + C ABI + wrapper mẫu.

Ngoài ra W10M đã ngừng hỗ trợ; cài đặt thực tế thường cần thiết bị ở chế độ Developer và APPX được ký. Nếu mục tiêu là hiệu năng và khả năng đóng gói dễ dàng, cân nhắc chạy bản desktop Windows (x86-64) trước.

## API chính

| Rust | C ABI | Mô tả |
| --- | --- | --- |
| `generate_sign_key` | `zalo_generate_sign_key` | MD5 hex của prefix + giá trị đã sort |
| `encrypt_aes_cbc` / `decrypt_aes_cbc` | `zalo_encrypt_aes_cbc` / `zalo_decrypt_aes_cbc` | AES-CBC PKCS7, IV zero, base64 |
| `decrypt_aes_gcm` | `zalo_decrypt_aes_gcm` | Giải mã payload `IV|tag|ciphertext` |
| `parse_frame` / `build_ping_frame` | `zalo_parse_frame` / `zalo_build_ping_frame` | Đóng/gỡ frame 4-byte header |

Mọi chuỗi trả về từ C ABI phải giải phóng bằng `zalo_free_string`; buffer byte bằng `zalo_free_bytes`.

## Phạm vi và pháp lý

Chỉ dùng cho nghiên cứu, tương tác và bảo tồn trên artifact bạn có quyền phân tích. Không dùng để copy code/tài sản độc quyền hay vượt qua bản quyền. REA được cài ở máy này nhưng **chưa chạy trên APK nào** vì chưa có file target.

## Bước tiếp theo

1. Đối chiếu `generate_sign_key` với `generateSignatureNative` (`libnative_utils.so`) bằng cách chạy thư viện ARM trên thiết bị hoặc emulator có quyền.
2. Hoàn thiện `nativeI420Scale` và `nativeI420ToARGB/ABGR`, mỗi hàm có vector tham chiếu riêng.
3. Đo benchmark Rust vs `libzvision-jni.so`/libyuv trên cùng input trước khi khẳng định nhanh hơn.
4. Tích hợp staticlib vào UWP và thử đóng gói APPX trên máy Windows (ARM tier 3).

Công cụ phân tích đã dùng: REA 6.10.1 với JADX headless MCP 0.7.1 (JAR đã kiểm tra SHA-256). Xem `docs/apk-findings.md` để có Evidence ID và giới hạn của từng kết luận.
