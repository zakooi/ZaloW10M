//! Prints fixed vectors so CI can compare the Rust core with the C# app code.
use zalo_core::{build_ping_frame, encrypt_aes_cbc, generate_sign_key, parse_frame};

const KEY: &str = "MDEyMzQ1Njc4OWFiY2RlZg==";

fn main() {
    let params = [
        ("computer_name", "Web"),
        ("imei", "abc123"),
        ("language", "vi"),
        ("ts", "1700000000000"),
    ];
    println!("sign={}", generate_sign_key(&params));
    println!("cbc={}", encrypt_aes_cbc("hello", KEY).unwrap());

    let ping = build_ping_frame(123);
    println!(
        "ping={}",
        ping.iter().map(|b| format!("{:02x}", b)).collect::<String>()
    );
    println!("payload={}", parse_frame(&ping, None).unwrap().payload);

    let mut b64_frame = vec![1u8, 245, 1, 1];
    b64_frame.extend_from_slice(br#"{"encrypt":1,"data":"aGVsbG8="}"#);
    println!("b64={}", parse_frame(&b64_frame, None).unwrap().payload);
    // AES-GCM is left out: .NET only accepts 12-byte nonces, this format uses 16.
}
