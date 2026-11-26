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

/// Scale every I420 plane with bilinear sampling. Destination pixel centres
/// are mapped back to source coordinates; samples outside the source are
/// clamped. Dimension changes keep the I420 layout, so both dimensions must be
/// even. Returns the scaled bytes together with the new width and height.
///
/// This is a clean-room definition of the scale behaviour, not a byte-for-byte
/// match of libyuv's `I420Scale` filter.
pub fn i420_scale(
    i420: &[u8],
    width: usize,
    height: usize,
    new_width: usize,
    new_height: usize,
) -> Result<(Vec<u8>, usize, usize), YuvError> {
    validate_i420(i420.len(), width, height)?;
    if new_width == 0 || new_height == 0 || new_width % 2 != 0 || new_height % 2 != 0 {
        return Err(YuvError::InvalidDimensions);
    }
    let y_len = width * height;
    let cw = width / 2;
    let ch = height / 2;
    let c_len = cw * ch;
    let ncw = new_width / 2;
    let nch = new_height / 2;

    let mut out = Vec::with_capacity(new_width * new_height + 2 * ncw * nch);
    out.extend_from_slice(&scale_plane(
        &i420[..y_len],
        width,
        height,
        new_width,
        new_height,
    ));
    out.extend_from_slice(&scale_plane(&i420[y_len..y_len + c_len], cw, ch, ncw, nch));
    out.extend_from_slice(&scale_plane(&i420[y_len + c_len..], cw, ch, ncw, nch));
    Ok((out, new_width, new_height))
}

fn scale_plane(src: &[u8], sw: usize, sh: usize, dw: usize, dh: usize) -> Vec<u8> {
    let mut out = vec![0u8; dw * dh];
    for y in 0..dh {
        let fy = (y as f32 + 0.5) * sh as f32 / dh as f32 - 0.5;
        let sy = fy.clamp(0.0, (sh - 1) as f32);
        let y0 = sy as usize;
        let y1 = (y0 + 1).min(sh - 1);
        let wy = sy - y0 as f32;
        for x in 0..dw {
            let fx = (x as f32 + 0.5) * sw as f32 / dw as f32 - 0.5;
            let sx = fx.clamp(0.0, (sw - 1) as f32);
            let x0 = sx as usize;
            let x1 = (x0 + 1).min(sw - 1);
            let wx = sx - x0 as f32;
            let top = src[y0 * sw + x0] as f32 * (1.0 - wx) + src[y0 * sw + x1] as f32 * wx;
            let bottom = src[y1 * sw + x0] as f32 * (1.0 - wx) + src[y1 * sw + x1] as f32 * wx;
            out[y * dw + x] = (top * (1.0 - wy) + bottom * wy).round() as u8;
        }
    }
    out
}

fn clamp_u8(value: i32) -> u8 {
    value.clamp(0, 255) as u8
}

/// Convert I420 to packed ARGB (bytes in alpha, red, green, blue order) using
/// the BT.601 limited-range transform. Alpha is always 255.
///
/// The byte order is defined here for our clean-room implementation; it has
/// not been byte-compared with the native `nativeI420ToARGB` output.
pub fn i420_to_argb(i420: &[u8], width: usize, height: usize) -> Result<Vec<u8>, YuvError> {
    validate_i420(i420.len(), width, height)?;
    let y_len = width * height;
    let cw = width / 2;
    let c_len = cw * (height / 2);
    let mut out = vec![0u8; y_len * 4];

    for y in 0..height {
        for x in 0..width {
            let luma = i420[y * width + x] as i32 - 16;
            let ci = (y / 2) * cw + x / 2;
            let u = i420[y_len + ci] as i32 - 128;
            let v = i420[y_len + c_len + ci] as i32 - 128;
            let o = (y * width + x) * 4;
            out[o] = 255;
            out[o + 1] = clamp_u8((298 * luma + 409 * v + 128) >> 8);
            out[o + 2] = clamp_u8((298 * luma - 100 * u - 208 * v + 128) >> 8);
            out[o + 3] = clamp_u8((298 * luma + 516 * u + 128) >> 8);
        }
    }
    Ok(out)
}

/// Same conversion as [`i420_to_argb`], with bytes in alpha, blue, green, red
/// order.
pub fn i420_to_abgr(i420: &[u8], width: usize, height: usize) -> Result<Vec<u8>, YuvError> {
    let mut out = i420_to_argb(i420, width, height)?;
    for pixel in out.chunks_exact_mut(4) {
        pixel.swap(1, 3);
    }
    Ok(out)
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

    fn solid_i420(width: usize, height: usize, y: u8, u: u8, v: u8) -> Vec<u8> {
        let mut out = vec![y; width * height];
        out.extend(std::iter::repeat_n(u, (width / 2) * (height / 2)));
        out.extend(std::iter::repeat_n(v, (width / 2) * (height / 2)));
        out
    }

    #[test]
    fn i420_to_argb_matches_reference_colours() {
        let cases: [(u8, u8, u8, [u8; 4]); 5] = [
            (16, 128, 128, [255, 0, 0, 0]),
            (235, 128, 128, [255, 255, 255, 255]),
            (81, 90, 240, [255, 255, 0, 0]),
            (150, 43, 21, [255, 0, 255, 0]),
            (41, 240, 110, [255, 0, 0, 255]),
        ];
        for (y, u, v, expected) in cases {
            let frame = solid_i420(2, 2, y, u, v);
            let argb = i420_to_argb(&frame, 2, 2).unwrap();
            assert_eq!(&argb[..4], &expected);
            assert!(argb.chunks_exact(4).all(|px| px == expected));
        }
    }

    #[test]
    fn i420_to_abgr_swaps_red_and_blue() {
        let frame = solid_i420(2, 2, 81, 90, 240);
        assert_eq!(&i420_to_abgr(&frame, 2, 2).unwrap()[..4], &[255, 0, 0, 255]);
    }

    #[test]
    fn i420_scale_matches_reference_vectors() {
        let (down, dw, dh) = i420_scale(&I420, 4, 4, 2, 2).unwrap();
        assert_eq!((dw, dh), (2, 2));
        assert_eq!(down, vec![3, 5, 11, 13, 102, 202]);

        let (up, uw, uh) = i420_scale(&I420, 4, 4, 8, 8).unwrap();
        assert_eq!((uw, uh), (8, 8));
        assert_eq!(up.len(), 8 * 8 + 2 * 4 * 4);
        assert_eq!(&up[..16], &[
            0, 0, 1, 1, 2, 2, 3, 3, 1, 1, 2, 2, 3, 3, 4, 4,
        ]);
        assert_eq!(&up[64..68], &[100, 100, 101, 101]);

        let (mixed, mw, mh) = i420_scale(&I420, 4, 4, 6, 4).unwrap();
        assert_eq!((mw, mh), (6, 4));
        assert_eq!(&mixed[..12], &[0, 1, 1, 2, 3, 3, 4, 5, 5, 6, 7, 7]);
    }

    #[test]
    fn scale_rejects_odd_dimensions() {
        assert_eq!(i420_scale(&I420, 4, 4, 3, 4), Err(YuvError::InvalidDimensions));
        assert_eq!(i420_scale(&I420, 4, 4, 4, 0), Err(YuvError::InvalidDimensions));
    }
}
