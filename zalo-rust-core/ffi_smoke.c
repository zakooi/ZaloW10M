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
    return 0;
}
