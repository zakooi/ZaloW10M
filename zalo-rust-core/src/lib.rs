//! Clean-room Rust core for the Zalo-compatible protocol surface used by the
//! UWP client. The algorithms here are specified from behaviour already
//! present in the local C# sources and from public standard primitives; no
//! APK code or assets are copied.
//!
//! The crate builds as a normal Rust library, a static library and a cdylib.
//! The `zalo_*` C ABI is intended for P/Invoke from the C# UWP shell.

use std::ffi::{CStr, CString};
use std::fmt;
use std::os::raw::{c_char, c_int};

use aes::Aes128;
use aes_gcm::aead::consts::U16;
use aes_gcm::aead::Aead;
use aes_gcm::{AesGcm, KeyInit, Nonce};
use base64::engine::general_purpose::STANDARD as BASE64;
use base64::Engine;
use cbc::cipher::block_padding::Pkcs7;
use cbc::cipher::{BlockDecryptMut, BlockEncryptMut, KeyIvInit};
use md5::{Digest, Md5};

pub mod yuv;

const SIGN_PREFIX: &str = "zsecuregetserverinfo";

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CoreError {
    NullPointer,
    InvalidUtf8,
    InvalidBase64,
    InvalidKeyLength,
    InvalidFrame,
    MissingCipherKey,
    Crypto,
}

impl fmt::Display for CoreError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            CoreError::NullPointer => "null pointer",
            CoreError::InvalidUtf8 => "invalid UTF-8",
            CoreError::InvalidBase64 => "invalid base64",
            CoreError::InvalidKeyLength => "key must be 16, 24 or 32 bytes",
            CoreError::InvalidFrame => "frame is shorter than its 4-byte header",
            CoreError::MissingCipherKey => "encrypted frame needs a cipher key",
            CoreError::Crypto => "cryptographic operation failed",
        })
    }
}

impl std::error::Error for CoreError {}

/// A decoded protocol frame. `cmd` and `sub_cmd` keep the same numeric values
/// as the C# `ZaloProtocol` constants.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Frame {
    pub version: u8,
    pub cmd: u16,
    pub sub_cmd: u8,
    pub payload: String,
}

/// Sort parameters by key, concatenate their values and MD5 the prefixed raw
/// string, matching `ZaloCrypto.GenerateSignKey`.
pub fn generate_sign_key(params: &[(&str, &str)]) -> String {
    let mut sorted: Vec<(&str, &str)> = params.to_vec();
    sorted.sort_by(|a, b| a.0.cmp(b.0));
    let mut raw = String::from(SIGN_PREFIX);
    for (_, value) in sorted {
        raw.push_str(value);
    }
    format!("{:x}", Md5::digest(raw.as_bytes()))
}

macro_rules! cbc_encrypt {
    ($cipher:ty, $plain:expr, $key:expr) => {{
        type Enc = cbc::Encryptor<$cipher>;
        let cipher = Enc::new_from_slices($key, &[0u8; 16]).map_err(|_| CoreError::InvalidKeyLength)?;
        Ok(cipher.encrypt_padded_vec_mut::<Pkcs7>($plain))
    }};
}

macro_rules! cbc_decrypt {
    ($cipher:ty, $data:expr, $key:expr) => {{
        type Dec = cbc::Decryptor<$cipher>;
        let cipher = Dec::new_from_slices($key, &[0u8; 16]).map_err(|_| CoreError::InvalidKeyLength)?;
        cipher
            .decrypt_padded_vec_mut::<Pkcs7>($data)
            .map_err(|_| CoreError::Crypto)
    }};
}

/// AES-CBC with PKCS#7 padding and a zero IV, base64 in and out.
pub fn encrypt_aes_cbc(plain: &str, key_b64: &str) -> Result<String, CoreError> {
    let key = BASE64.decode(key_b64).map_err(|_| CoreError::InvalidBase64)?;
    let bytes = match key.len() {
        16 => cbc_encrypt!(Aes128, plain.as_bytes(), &key)?,
        24 => cbc_encrypt!(aes::Aes192, plain.as_bytes(), &key)?,
        32 => cbc_encrypt!(aes::Aes256, plain.as_bytes(), &key)?,
        _ => return Err(CoreError::InvalidKeyLength),
    };
    Ok(BASE64.encode(bytes))
}

pub fn decrypt_aes_cbc(data_b64: &str, key_b64: &str) -> Result<String, CoreError> {
    let key = BASE64.decode(key_b64).map_err(|_| CoreError::InvalidBase64)?;
    let data = BASE64.decode(data_b64).map_err(|_| CoreError::InvalidBase64)?;
    let bytes = match key.len() {
        16 => cbc_decrypt!(Aes128, &data, &key)?,
        24 => cbc_decrypt!(aes::Aes192, &data, &key)?,
        32 => cbc_decrypt!(aes::Aes256, &data, &key)?,
        _ => return Err(CoreError::InvalidKeyLength),
    };
    String::from_utf8(bytes).map_err(|_| CoreError::InvalidUtf8)
}

type Gcm128 = AesGcm<Aes128, U16>;
type Gcm256 = AesGcm<aes::Aes256, U16>;

macro_rules! gcm_decrypt {
    ($cipher:ty, $nonce:expr, $data:expr, $key:expr) => {{
        let cipher = <$cipher>::new_from_slice($key).map_err(|_| CoreError::InvalidKeyLength)?;
        let nonce = Nonce::<U16>::from_slice($nonce);
        cipher.decrypt(nonce, $data).map_err(|_| CoreError::Crypto)
    }};
}

/// Decrypt the shipped AES-GCM layout: 16-byte IV, 16-byte tag, then
/// ciphertext. Returns UTF-8 plaintext.
pub fn decrypt_aes_gcm(payload: &[u8], cipher_key_b64: &str) -> Result<String, CoreError> {
    if payload.len() < 32 {
        return Err(CoreError::Crypto);
    }
    let key = BASE64.decode(cipher_key_b64).map_err(|_| CoreError::InvalidBase64)?;
    let nonce = &payload[..16];
    let tag = &payload[16..32];
    let mut sealed = Vec::with_capacity(payload.len() - 16);
    sealed.extend_from_slice(&payload[32..]);
    sealed.extend_from_slice(tag);
    let bytes = match key.len() {
        16 => gcm_decrypt!(Gcm128, nonce, sealed.as_slice(), &key)?,
        32 => gcm_decrypt!(Gcm256, nonce, sealed.as_slice(), &key)?,
        _ => return Err(CoreError::InvalidKeyLength),
    };
    String::from_utf8(bytes).map_err(|_| CoreError::InvalidUtf8)
}

/// Decode `version, cmd, sub_cmd` and the JSON payload of a frame. Field
/// `encrypt` follows the observed values: 0 plain, 1 base64, 2/3 AES-GCM.
pub fn parse_frame(buffer: &[u8], cipher_key: Option<&str>) -> Result<Frame, CoreError> {
    if buffer.len() < 4 {
        return Err(CoreError::InvalidFrame);
    }
    let version = buffer[0];
    let cmd = u16::from_le_bytes([buffer[1], buffer[2]]);
    let sub_cmd = buffer[3];
    if buffer.len() == 4 {
        return Ok(Frame { version, cmd, sub_cmd, payload: String::new() });
    }

    let raw = std::str::from_utf8(&buffer[4..]).map_err(|_| CoreError::InvalidUtf8)?;
    let payload = if let Ok(value) = serde_json::from_str::<serde_json::Value>(raw) {
        if value.is_object() {
            match value.get("encrypt").and_then(|v| v.as_i64()) {
                Some(0) => value
                    .get("data")
                    .and_then(|v| v.as_str())
                    .unwrap_or(raw)
                    .to_string(),
                Some(1) => {
                    let encoded = value.get("data").and_then(|v| v.as_str()).unwrap_or("");
                    let decoded = BASE64.decode(encoded).map_err(|_| CoreError::InvalidBase64)?;
                    String::from_utf8(decoded).map_err(|_| CoreError::InvalidUtf8)?
                }
                Some(2) | Some(3) => {
                    let key = cipher_key.ok_or(CoreError::MissingCipherKey)?;
                    let encoded = value.get("data").and_then(|v| v.as_str()).unwrap_or("");
                    let decoded = BASE64.decode(encoded).map_err(|_| CoreError::InvalidBase64)?;
                    decrypt_aes_gcm(&decoded, key)?
                }
                _ => raw.to_string(),
            }
        } else {
            raw.to_string()
        }
    } else {
        raw.to_string()
    };

    Ok(Frame { version, cmd, sub_cmd, payload })
}

/// Build the 4-byte header plus `{"eventId":<id>}` ping frame.
pub fn build_ping_frame(event_id: i64) -> Vec<u8> {
    let json = format!("{{\"eventId\":{}}}", event_id);
    let mut frame = Vec::with_capacity(4 + json.len());
    frame.extend_from_slice(&[1, 2, 0, 1]);
    frame.extend_from_slice(json.as_bytes());
    frame
}

// ---------------------------------------------------------------------------
// C ABI for P/Invoke. All input strings are UTF-8 NUL-terminated. Output
// strings must be released with `zalo_free_string`; byte buffers with
// `zalo_free_bytes`. Functions return 0 on success and -1 on error.
// ---------------------------------------------------------------------------

unsafe fn input_str<'a>(ptr: *const c_char) -> Result<&'a str, CoreError> {
    if ptr.is_null() {
        return Err(CoreError::NullPointer);
    }
    CStr::from_ptr(ptr).to_str().map_err(|_| CoreError::InvalidUtf8)
}

fn out_string(out: *mut *mut c_char, value: Result<String, CoreError>) -> c_int {
    if out.is_null() {
        return -1;
    }
    unsafe { *out = std::ptr::null_mut() };
    match value {
        Ok(value) => {
            let c = match CString::new(value) {
                Ok(c) => c,
                Err(_) => return -1,
            };
            unsafe { *out = c.into_raw() };
            0
        }
        Err(_) => -1,
    }
}

#[no_mangle]
pub extern "C" fn zalo_generate_sign_key(params_json: *const c_char, out: *mut *mut c_char) -> c_int {
    let result = (|| {
        let json = unsafe { input_str(params_json)? };
        let value: serde_json::Value =
            serde_json::from_str(json).map_err(|_| CoreError::InvalidFrame)?;
        let object = value.as_object().ok_or(CoreError::InvalidFrame)?;
        let mut params: Vec<(String, String)> = object
            .iter()
            .map(|(k, v)| Ok((k.clone(), v.as_str().ok_or(CoreError::InvalidFrame)?.to_string())))
            .collect::<Result<_, CoreError>>()?;
        params.sort_by(|a, b| a.0.cmp(&b.0));
        let borrowed: Vec<(&str, &str)> = params.iter().map(|(k, v)| (k.as_str(), v.as_str())).collect();
        Ok(generate_sign_key(&borrowed))
    })();
    out_string(out, result)
}

#[no_mangle]
pub extern "C" fn zalo_encrypt_aes_cbc(
    plain: *const c_char,
    key_b64: *const c_char,
    out: *mut *mut c_char,
) -> c_int {
    let result = (|| {
        let plain = unsafe { input_str(plain)? };
        let key = unsafe { input_str(key_b64)? };
        encrypt_aes_cbc(plain, key)
    })();
    out_string(out, result)
}

#[no_mangle]
pub extern "C" fn zalo_decrypt_aes_cbc(
    data_b64: *const c_char,
    key_b64: *const c_char,
    out: *mut *mut c_char,
) -> c_int {
    let result = (|| {
        let data = unsafe { input_str(data_b64)? };
        let key = unsafe { input_str(key_b64)? };
        decrypt_aes_cbc(data, key)
    })();
    out_string(out, result)
}

#[no_mangle]
pub extern "C" fn zalo_decrypt_aes_gcm(
    payload: *const u8,
    payload_len: usize,
    cipher_key_b64: *const c_char,
    out: *mut *mut c_char,
) -> c_int {
    let result = (|| {
        if payload.is_null() {
            return Err(CoreError::NullPointer);
        }
        let bytes = unsafe { std::slice::from_raw_parts(payload, payload_len) };
        let key = unsafe { input_str(cipher_key_b64)? };
        decrypt_aes_gcm(bytes, key)
    })();
    out_string(out, result)
}

#[no_mangle]
pub extern "C" fn zalo_parse_frame(
    buffer: *const u8,
    buffer_len: usize,
    cipher_key: *const c_char,
    out: *mut *mut c_char,
) -> c_int {
    let result = (|| {
        if buffer.is_null() {
            return Err(CoreError::NullPointer);
        }
        let bytes = unsafe { std::slice::from_raw_parts(buffer, buffer_len) };
        let key = if cipher_key.is_null() {
            None
        } else {
            Some(unsafe { input_str(cipher_key)? })
        };
        let frame = parse_frame(bytes, key)?;
        serde_json::to_string(&serde_json::json!({
            "version": frame.version,
            "cmd": frame.cmd,
            "sub_cmd": frame.sub_cmd,
            "payload": frame.payload,
        }))
        .map_err(|_| CoreError::InvalidFrame)
    })();
    out_string(out, result)
}

#[no_mangle]
pub extern "C" fn zalo_build_ping_frame(
    event_id: i64,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    if out.is_null() || out_len.is_null() {
        return -1;
    }
    unsafe {
        *out = std::ptr::null_mut();
        *out_len = 0;
        let bytes = build_ping_frame(event_id);
        let ptr = libc::malloc(bytes.len()) as *mut u8;
        if ptr.is_null() {
            return -1;
        }
        std::ptr::copy_nonoverlapping(bytes.as_ptr(), ptr, bytes.len());
        *out = ptr;
        *out_len = bytes.len();
    }
    0
}

unsafe fn out_bytes(out: *mut *mut u8, out_len: *mut usize, bytes: &[u8]) -> c_int {
    if out.is_null() || out_len.is_null() {
        return -1;
    }
    *out = std::ptr::null_mut();
    *out_len = 0;
    let ptr = libc::malloc(bytes.len()) as *mut u8;
    if ptr.is_null() {
        return -1;
    }
    std::ptr::copy_nonoverlapping(bytes.as_ptr(), ptr, bytes.len());
    *out = ptr;
    *out_len = bytes.len();
    0
}

#[no_mangle]
pub extern "C" fn zalo_i420_to_nv21(
    i420: *const u8,
    len: usize,
    width: usize,
    height: usize,
    out: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    if i420.is_null() {
        return -1;
    }
    let bytes = unsafe { std::slice::from_raw_parts(i420, len) };
    match yuv::i420_to_nv21(bytes, width, height) {
        Ok(result) => unsafe { out_bytes(out, out_len, &result) },
        Err(_) => -1,
    }
}

#[no_mangle]
pub extern "C" fn zalo_rotate_i420(
    i420: *const u8,
    len: usize,
    width: usize,
    height: usize,
    degrees: usize,
    out: *mut *mut u8,
    out_len: *mut usize,
    out_width: *mut usize,
    out_height: *mut usize,
) -> c_int {
    if i420.is_null() || out_width.is_null() || out_height.is_null() {
        return -1;
    }
    let bytes = unsafe { std::slice::from_raw_parts(i420, len) };
    match yuv::rotate_i420(bytes, width, height, degrees) {
        Ok((result, new_width, new_height)) => {
            unsafe {
                *out_width = new_width;
                *out_height = new_height;
            }
            unsafe { out_bytes(out, out_len, &result) }
        }
        Err(_) => -1,
    }
}

#[no_mangle]
pub extern "C" fn zalo_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            drop(CString::from_raw(ptr));
        }
    }
}

#[no_mangle]
pub extern "C" fn zalo_free_bytes(ptr: *mut u8, len: usize) {
    if !ptr.is_null() && len > 0 {
        unsafe { libc::free(ptr as *mut libc::c_void) };
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const KEY_B64: &str = "MDEyMzQ1Njc4OWFiY2RlZg==";

    #[test]
    fn sign_key_matches_reference() {
        let params = [
            ("computer_name", "Web"),
            ("imei", "abc123"),
            ("language", "vi"),
            ("ts", "1700000000000"),
        ];
        assert_eq!(generate_sign_key(&params), "6c5d80bd07783d9b32e1a39690d1d451");
    }

    #[test]
    fn aes_cbc_matches_openssl_vector() {
        let cipher = encrypt_aes_cbc("hello", KEY_B64).unwrap();
        assert_eq!(cipher, "Z0x+8454yr2c7JwSWCOmOQ==");
        assert_eq!(decrypt_aes_cbc(&cipher, KEY_B64).unwrap(), "hello");
    }

    #[test]
    fn aes_gcm_matches_reference_vector() {
        let payload = BASE64
            .decode("AAAAAAAAAAAAAAAAAAAAAOuWM+uT+JDgGB7TPkbeBevon7cx68ulQCimsg==")
            .unwrap();
        assert_eq!(decrypt_aes_gcm(&payload, KEY_B64).unwrap(), "hello world");
    }

    #[test]
    fn ping_frame_roundtrip() {
        let frame = build_ping_frame(123);
        let decoded = parse_frame(&frame, None).unwrap();
        assert_eq!(decoded.version, 1);
        assert_eq!(decoded.cmd, 2);
        assert_eq!(decoded.sub_cmd, 1);
        assert_eq!(decoded.payload, "{\"eventId\":123}");
    }

    #[test]
    fn plain_json_frame_payload() {
        let json = br#"{"encrypt":0,"data":"{\"msgId\":\"m1\"}"}"#;
        let mut frame = vec![1u8, 245, 1, 1];
        frame.extend_from_slice(json);
        let decoded = parse_frame(&frame, None).unwrap();
        assert_eq!(decoded.cmd, 501);
        assert_eq!(decoded.payload, "{\"msgId\":\"m1\"}");
    }

    #[test]
    fn short_frame_is_rejected() {
        assert_eq!(parse_frame(&[1, 2, 0], None), Err(CoreError::InvalidFrame));
    }
}
