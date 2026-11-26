# Zalo APK — findings ledger (REA)

Artifact: `Zalo_26.11.01_APKPure.xapk`, downloaded from the user-supplied
APKPure/WinUDF URL (third-party source, authenticity **not** established).

| Artifact | SHA-256 | Evidence |
| --- | --- | --- |
| XAPK | `9bb53aa18de646c6d6d74399e2625763dc933e8d1c97ef1d9fe4cf733718ce8a` | `ev_02ee3e618f6113e017c4a640a99547ef8dd2b49c32cab1d84bdcf30cb85594bc` (inventory summary `ev_38f048fa38c28c8f6004f6d6870be9ff676967fcce33ab16538dc1e2d2b16b56`) |
| `com.zing.zalo.apk` | `d85b94a7a9b8c5d2be58a29bbb0c31aa90d571f99615917f20f0a07b05d437cd` | signature `ev_93a3c21d53e567a5b0bebf506648f5f96988b33143f48ca1781de90a67a8fdbf` |
| `config.armeabi_v7a.apk` | `4b4cc884c08ee07e6e3f0c4d16bad7ab887f4746de68aca8c0b7ebc94bab70fe` | signature `ev_57f8008c9280c643678f129e2a2bf77b2791e289fd240cf9a19ac52e44f3e95d` |
| Manifest/classes | base APK | `ev_8428d3d8759b35a0ea4825ba939bbbe20da7ab40a255e3beacbad6ee61f9bd8e` |
| `NativeWebSocket` class | base APK | `ev_bbf2453928d9bb6da83c3b4668ecc9ae3ca06207dd2e2fc0df2ea79251c5ea1a` |
| JNI mapping (derived merged APK) | `/tmp/opencode/zalo_merged_for_jni.apk` | `ev_4885c6a1d46c3a7895cd3ceb31aff12d99cfb201fac5064acb20499ca109ee0c` |

## Observations

- XAPK contains base APK, `config.armeabi_v7a.apk`, `config.xhdpi.apk`, icon and
  `manifest.json`. Version 26.11.01 (`261101901`), min SDK 28, target SDK 37.
- Base APK: **46,897 classes**, 8,906 resources, 6 DEX files. Manifest declares
  ~80 permissions including camera, microphone, contacts, location, NFC and
  foreground services. APK requires ABI and density splits.
- Signing: base and ABI split both declare v2 + v3 (no v1), same certificate
  SHA-256 `d86efe151e09bf4ca8440cb3bfa0a81be2544f70c78587daf0266dfca2fa25df`,
  source-stamp certificate `3257d599a49d2c961a471ca9843f59d341a405884583fc087df4237b733bbd6d`,
  `self_consistent: true`. **Signature verification was not performed**; this is
  only the declared structure, not proof the file is genuine Zalo.
- The ABI split carries 44 `armeabi-v7a` native libraries, including
  `libznetwork.so` (3.0 MB), `libzalosqlite.so` (4.0 MB), `libzproto_sync_ffi.so`,
  `libzproto_trust_ffi.so`, `libnative_utils.so`, `libyuv.so`, `libwebp.so`,
  `libopus.so`, `libx264.so`, FFmpeg libraries and `libzvision-jni.so`.

## JNI → library mapping (static, derived APK)

The merged APK is a **derived artifact** (base APK + split `.so` entries) built
only for structural mapping; its digest differs from the downloaded XAPK.

| Java class | Native methods mapped | Library |
| --- | --- | --- |
| `com.zing.zalocore.utils.cryptology.CoreUtils` | `generateSignatureNative`, `encodeHttpParamsNative`, `getZCIDNative`, `initHttpParamsSNative` | `libnative_utils.so` |
| `com.zing.zalocore.connection.socket.NativeWebSocket` | `nativeConnect`, `nativeDisconnect`, `nativeSendRequest` | `libznetwork.so` |
| `com.zing.zalocore.connection.socket.NativeSocket` | 17 of 26 methods (connectivity, timeouts, DoH, HTTP/3, QoS, ping) | `libznetwork.so` |
| `com.zing.zalocore.connection.socket.NativeE2ee` | 12 of 32 methods (trust device list, GCM buffer/file decrypt, QR decode) | `libznetwork.so` |
| `com.zing.zalo.db.SQLiteDatabase` | all 13 methods (open/close/exec/prepare/transactions/backup) | `libzalosqlite.so` |
| `com.zing.zalo.zvision.utils.YuvImageHelperKt` | `nativeI420Rotate`, `nativeI420Scale`, `nativeI420ToABGR`, `nativeI420ToARGB`, `nativeI420ToNV21`, `nativeNV21ToI420` | `libzvision-jni.so` |

For `NativeWebSocket`, JNI exports were observed at addresses `0x192acd`,
`0x192d41`, `0x1927b9` in `libznetwork.so`. `matched` means an observed static
candidate, not proof of loading, `RegisterNatives` execution or invocation.

## Inferences and unknowns

- **Inference:** the CPU-heavy, well-specified image operations are the best
  first Rust port: fixed struct layouts (I420/NV21), deterministic byte output,
  easy differential tests. Implemented in `src/yuv.rs` with 10 passing tests.
- **Inference:** the local C# `ZaloCrypto.GenerateSignKey` corresponds in purpose
  to `CoreUtils.generateSignatureNative` in `libnative_utils.so`; the Rust core
  already implements the MD5/sign behavior from the local source, but it has
  **not** been byte-compared against the native implementation.
- **Unknown:** E2EE internals in `libznetwork.so` are not reconstructed; no
  attempt was made to recover keys, secrets or proprietary algorithms.
- **Unknown:** `libzalosqlite.so` encryption/DB schema was not analysed.
- **Unknown:** runtime registration, dynamic loading and obfuscated strings are
  not recovered by static JNI binding analysis.
- Static analysis only: the APK was never executed; no device, emulator or adb
  was involved.

## Scope

Research/interoperability only. No APK code, assets or native binaries were
copied into the Rust crate; the Rust code is written from public format
specifications and the existing local C# sources.
