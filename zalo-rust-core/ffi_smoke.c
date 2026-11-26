/* Minimal C consumer of the zalo_core C ABI.
 * Build (Linux):
 *   cargo build --release
 *   gcc ffi_smoke.c target/release/libzalo_core.a -ldl -lpthread -lm -o ffi_smoke
 *   ./ffi_smoke
 */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

typedef int (*sign_fn)(const char *, char **);
typedef int (*ping_fn)(int64_t, uint8_t **, size_t *);
typedef void (*free_str_fn)(char *);
typedef void (*free_bytes_fn)(uint8_t *, size_t);

extern int zalo_generate_sign_key(const char *params_json, char **out);
extern int zalo_build_ping_frame(int64_t event_id, uint8_t **out, size_t *out_len);
extern int zalo_i420_to_argb(const uint8_t *i420, size_t len, size_t width, size_t height,
                             uint8_t **out, size_t *out_len);
extern int zalo_i420_scale(const uint8_t *i420, size_t len, size_t width, size_t height,
                           size_t new_width, size_t new_height,
                           uint8_t **out, size_t *out_len,
                           size_t *out_width, size_t *out_height);
extern void zalo_free_string(char *ptr);
extern void zalo_free_bytes(uint8_t *ptr, size_t len);

int main(void) {
    char *sign = NULL;
    const char *params = "{\"computer_name\":\"Web\",\"imei\":\"abc123\",\"language\":\"vi\",\"ts\":\"1700000000000\"}";
    if (zalo_generate_sign_key(params, &sign) != 0) {
        fprintf(stderr, "generate_sign_key failed\n");
        return 1;
    }
    printf("signkey=%s\n", sign);
    zalo_free_string(sign);

    uint8_t *frame = NULL;
    size_t len = 0;
    if (zalo_build_ping_frame(123, &frame, &len) != 0) {
        fprintf(stderr, "build_ping_frame failed\n");
        return 1;
    }
    printf("ping(%zu)=", len);
    for (size_t i = 0; i < len; i++) {
        printf("%02x", frame[i]);
    }
    printf("\n");
    zalo_free_bytes(frame, len);

    /* 2x2 black I420: four luma samples plus one U and one V. */
    const uint8_t black[6] = {16, 16, 16, 16, 128, 128};
    uint8_t *argb = NULL;
    size_t argb_len = 0;
    if (zalo_i420_to_argb(black, sizeof black, 2, 2, &argb, &argb_len) != 0 ||
        argb_len != 16) {
        fprintf(stderr, "i420_to_argb failed\n");
        return 1;
    }
    printf("argb(0)=");
    for (size_t i = 0; i < 4; i++) printf("%02x", argb[i]);
    printf("\n");
    zalo_free_bytes(argb, argb_len);

    uint8_t *scaled = NULL;
    size_t scaled_len = 0, scaled_w = 0, scaled_h = 0;
    if (zalo_i420_scale(black, sizeof black, 2, 2, 4, 4,
                        &scaled, &scaled_len, &scaled_w, &scaled_h) != 0 ||
        scaled_len != 24 || scaled_w != 4 || scaled_h != 4) {
        fprintf(stderr, "i420_scale failed\n");
        return 1;
    }
    printf("scaled=%zux%zu bytes=%zu\n", scaled_w, scaled_h, scaled_len);
    zalo_free_bytes(scaled, scaled_len);
    return 0;
}
