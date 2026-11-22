/* Host feasibility probe, not compiler/runtime implementation.
 * Let the kernel choose disjoint reservations. Never replace existing mappings.
 * PROT_NONE reserves addresses; mprotect enables selected pages. Actual writes
 * cause demand backing. Sparse access avoids touching the full virtual span.
 */
#include <errno.h>
#include <inttypes.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>

typedef struct {
    unsigned char *base;
    size_t bytes;
    size_t stride;
} Field;

int main(void)
{
    const uint64_t slots = UINT64_C(1) << 32;
    const uint64_t indices[] = {0, UINT64_C(1) << 31, (UINT64_C(1) << 32) - 1};
    Field fields[] = {{NULL, 0, 1}, {NULL, 0, 4}, {NULL, 0, 8}};
    const long page_result = sysconf(_SC_PAGESIZE);
    int status = 1;
    unsigned char *heap = NULL;
    if (page_result <= 0) {
        fprintf(stderr, "Cannot obtain page size\n");
        return 1;
    }
    const size_t page = (size_t)page_result;
    heap = malloc(1024 * 1024);
    if (heap == NULL) {
        fprintf(stderr, "Ordinary heap allocation failed\n");
        return 1;
    }
    memset(heap, 0x5a, 1024 * 1024);
    printf("page=%zu heap=%p\n", page, (void *)heap);
    for (size_t f = 0; f < 3; ++f) {
        Field *field = &fields[f];
        if (slots > SIZE_MAX / field->stride) {
            fprintf(stderr, "Reservation byte size is not representable\n");
            goto cleanup;
        }
        field->bytes = (size_t)slots * field->stride;
        void *reserved = mmap(NULL, field->bytes, PROT_NONE,
                              MAP_PRIVATE | MAP_ANON, -1, 0);
        if (reserved == MAP_FAILED) {
            fprintf(stderr, "mmap stride=%zu: %s\n", field->stride, strerror(errno));
            goto cleanup;
        }
        field->base = reserved;
        printf("stride=%zu bytes=%zu base=%p\n", field->stride,
               field->bytes, (void *)field->base);
        for (size_t i = 0; i < 3; ++i) {
            const size_t offset = (size_t)indices[i] * field->stride;
            const size_t page_offset = offset - offset % page;
            if (mprotect(field->base + page_offset, page,
                         PROT_READ | PROT_WRITE) != 0) {
                fprintf(stderr, "mprotect: %s\n", strerror(errno));
                goto cleanup;
            }
            /* memcpy avoids alignment/type-punning assumptions. All strides
             * divide the host page size, including the last selected slot. */
            const uint64_t value = UINT64_C(0x31) + f * 8 + i;
            memcpy(field->base + offset, &value, field->stride);
        }
    }
    /* Revisit after ALL reservations and protections: bases stay unchanged. */
    for (size_t f = 0; f < 3; ++f) {
        for (size_t i = 0; i < 3; ++i) {
            const size_t offset = (size_t)indices[i] * fields[f].stride;
            uint64_t actual = 0;
            const uint64_t expected = UINT64_C(0x31) + f * 8 + i;
            memcpy(&actual, fields[f].base + offset, fields[f].stride);
            if (actual != expected) {
                fprintf(stderr, "Sparse value mismatch\n");
                goto cleanup;
            }
        }
    }
    for (size_t i = 0; i < 1024 * 1024; ++i) {
        if (heap[i] != 0x5a) {
            fprintf(stderr, "Heap contents changed\n");
            goto cleanup;
        }
    }
    status = 0;
cleanup:
    for (size_t f = 0; f < 3; ++f) {
        if (fields[f].base != NULL) {
            if (munmap(fields[f].base, fields[f].bytes) != 0) {
                fprintf(stderr, "munmap: %s\n", strerror(errno));
                status = 1;
            }
        }
    }
    free(heap);
    if (status == 0) {
        printf("PASS: 52 GiB reserved simultaneously; 9 pages enabled and checked; heap preserved; all mappings released\n");
    }
    return status;
}
