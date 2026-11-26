//! YUV plane operations for the image path observed in the Zalo APK.
//!
//! REA mapped `com.zing.zalo.zvision.utils.YuvImageHelperKt.nativeI420*`
//! and `nativeNV21ToI420` to exported JNI symbols in
//! `lib/armeabi-v7a/libzvision-jni.so` (inside `config.armeabi_v7a.apk`).
//! These are behaviour specifications written from the public I420/NV21
//! layout, not copied code from the APK or from libyuv.
//!
//! I420: Y plane `w*h`, U plane `w/2*h/2`, V plane `w/2*h/2`.
//! NV21: Y plane `w*h`, then interleaved V,U pairs.

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum YuvError {
    InvalidDimensions,
    InvalidLength,
    InvalidRotation,
}

fn validate_i420(len: usize, width: usize, height: usize) -> Result<(), YuvError> {
    if width == 0 || height == 0 || width % 2 != 0 || height % 2 != 0 {
        return Err(YuvError::InvalidDimensions);
    }
    let expected = width * height + 2 * (width / 2) * (height / 2);
    if len != expected {
        return Err(YuvError::InvalidLength);
    }
    Ok(())
}

/// Convert I420 to NV21.
pub fn i420_to_nv21(i420: &[u8], width: usize, height: usize) -> Result<Vec<u8>, YuvError> {
    validate_i420(i420.len(), width, height)?;
    let y_len = width * height;
    let cw = width / 2;
    let ch = height / 2;
    let mut out = Vec::with_capacity(i420.len());
    out.extend_from_slice(&i420[..y_len]);
    let u_plane = y_len;
    let v_plane = y_len + cw * ch;
    for cy in 0..ch {
        for cx in 0..cw {
            out.push(i420[v_plane + cy * cw + cx]);
            out.push(i420[u_plane + cy * cw + cx]);
        }
    }
    Ok(out)
}

/// Convert NV21 to I420.
pub fn nv21_to_i420(nv21: &[u8], width: usize, height: usize) -> Result<Vec<u8>, YuvError> {
    validate_i420(nv21.len(), width, height)?;
    let y_len = width * height;
    let c_len = (width / 2) * (height / 2);
    let mut out = vec![0u8; nv21.len()];
    out[..y_len].copy_from_slice(&nv21[..y_len]);
    for i in 0..c_len {
        out[y_len + i] = nv21[y_len + 2 * i + 1];
        out[y_len + c_len + i] = nv21[y_len + 2 * i];
    }
    Ok(out)
}

fn rotate_plane(src: &[u8], width: usize, height: usize, degrees: usize) -> Vec<u8> {
    let mut out = vec![0u8; src.len()];
    match degrees {
        90 => {
            for y in 0..height {
                for x in 0..width {
                    out[x * height + (height - 1 - y)] = src[y * width + x];
                }
            }
        }
        180 => {
            for y in 0..height {
                for x in 0..width {
                    out[(height - 1 - y) * width + (width - 1 - x)] = src[y * width + x];
                }
            }
        }
        270 => {
            for y in 0..height {
                for x in 0..width {
                    out[(width - 1 - x) * height + y] = src[y * width + x];
                }
            }
        }
        _ => unreachable!(),
    }
    out
}

/// Rotate an I420 image clockwise by a multiple of 90 degrees.
/// Returns the rotated bytes together with the new width and height.
pub fn rotate_i420(
    i420: &[u8],
    width: usize,
    height: usize,
    degrees: usize,
) -> Result<(Vec<u8>, usize, usize), YuvError> {
    validate_i420(i420.len(), width, height)?;
    let deg = degrees % 360;
    if deg % 90 != 0 {
        return Err(YuvError::InvalidRotation);
    }
    if deg == 0 {
        return Ok((i420.to_vec(), width, height));
    }
    let y_len = width * height;
    let cw = width / 2;
    let ch = height / 2;
    let c_len = cw * ch;
    let ry = rotate_plane(&i420[..y_len], width, height, deg);
    let ru = rotate_plane(&i420[y_len..y_len + c_len], cw, ch, deg);
    let rv = rotate_plane(&i420[y_len + c_len..], cw, ch, deg);
    let mut out = Vec::with_capacity(i420.len());
    out.extend_from_slice(&ry);
    out.extend_from_slice(&ru);
    out.extend_from_slice(&rv);
    let (new_width, new_height) = if deg == 90 || deg == 270 {
        (height, width)
    } else {
        (width, height)
    };
    Ok((out, new_width, new_height))
}

#[cfg(test)]
mod tests {
    use super::*;

    const I420: [u8; 24] = [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 100, 101, 102, 103, 200, 201,
        202, 203,
    ];
    const NV21: [u8; 24] = [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 200, 100, 201, 101, 202, 102,
        203, 103,
    ];

    #[test]
    fn i420_to_nv21_matches_reference() {
        assert_eq!(i420_to_nv21(&I420, 4, 4).unwrap(), NV21);
    }

    #[test]
    fn nv21_to_i420_matches_reference() {
        assert_eq!(nv21_to_i420(&NV21, 4, 4).unwrap(), I420);
    }

    #[test]
    fn rotate_i420_matches_reference() {
        let cases: [(usize, [u8; 24], usize, usize); 3] = [
            (
                90,
                [
                    12, 8, 4, 0, 13, 9, 5, 1, 14, 10, 6, 2, 15, 11, 7, 3, 102, 100, 103, 101,
                    202, 200, 203, 201,
                ],
                4,
                4,
            ),
            (
                180,
                [
                    15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 103, 102, 101, 100,
                    203, 202, 201, 200,
                ],
                4,
                4,
            ),
            (
                270,
                [
                    3, 7, 11, 15, 2, 6, 10, 14, 1, 5, 9, 13, 0, 4, 8, 12, 101, 103, 100, 102,
                    201, 203, 200, 202,
                ],
                4,
                4,
            ),
        ];
        for (deg, expected, w, h) in cases {
            let (got, nw, nh) = rotate_i420(&I420, 4, 4, deg).unwrap();
            assert_eq!(got.as_slice(), &expected[..]);
            assert_eq!((nw, nh), (w, h));
        }
    }

    #[test]
    fn rejects_bad_input() {
        assert_eq!(i420_to_nv21(&I420, 3, 4), Err(YuvError::InvalidDimensions));
        assert_eq!(
            nv21_to_i420(&NV21[..20], 4, 4),
            Err(YuvError::InvalidLength)
        );
        assert_eq!(
            rotate_i420(&I420, 4, 4, 45),
            Err(YuvError::InvalidRotation)
        );
    }
}
