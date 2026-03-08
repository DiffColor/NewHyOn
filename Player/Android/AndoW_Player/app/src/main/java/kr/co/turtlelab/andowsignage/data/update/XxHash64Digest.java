package kr.co.turtlelab.andowsignage.data.update;

/**
 * 간단한 XXHash64 구현 (seed=0). Windows 플레이어의 XXHash 검증과 호환되도록 16자리 hex를 반환한다.
 * 참조: xxHash 알고리즘 공개 스펙
 */
public final class XxHash64Digest {

    private static final long PRIME1 = -7046029288634856825L;      // 11400714785074694791
    private static final long PRIME2 = -4417276706812531889L;      // 14029467366897019727
    private static final long PRIME3 = 1609587929392839161L;
    private static final long PRIME4 = -8796714831421723037L;      // 9650029242287828579
    private static final long PRIME5 = 2870177450012600261L;

    private final byte[] buffer = new byte[32];
    private int bufferSize;
    private long totalLen;
    private long v1;
    private long v2;
    private long v3;
    private long v4;
    private final long seed;

    public XxHash64Digest(long seed) {
        this.seed = seed;
        reset();
    }

    public void reset() {
        v1 = seed + PRIME1 + PRIME2;
        v2 = seed + PRIME2;
        v3 = seed;
        v4 = seed - PRIME1;
        totalLen = 0;
        bufferSize = 0;
    }

    public void update(byte[] data, int offset, int length) {
        if (data == null || length <= 0) {
            return;
        }
        totalLen += length;
        int idx = offset;
        int end = offset + length;

        if (bufferSize + length < 32) {
            System.arraycopy(data, idx, buffer, bufferSize, length);
            bufferSize += length;
            return;
        }

        if (bufferSize > 0) {
            int fill = 32 - bufferSize;
            System.arraycopy(data, idx, buffer, bufferSize, fill);
            process(buffer, 0);
            idx += fill;
            bufferSize = 0;
        }

        while (idx <= end - 32) {
            process(data, idx);
            idx += 32;
        }

        if (idx < end) {
            bufferSize = end - idx;
            System.arraycopy(data, idx, buffer, 0, bufferSize);
        }
    }

    private void process(byte[] data, int offset) {
        v1 = round(v1, getLong(data, offset));
        v2 = round(v2, getLong(data, offset + 8));
        v3 = round(v3, getLong(data, offset + 16));
        v4 = round(v4, getLong(data, offset + 24));
    }

    public long getValue() {
        long h64;
        if (totalLen >= 32) {
            h64 = Long.rotateLeft(v1, 1) + Long.rotateLeft(v2, 7) + Long.rotateLeft(v3, 12) + Long.rotateLeft(v4, 18);
            h64 = mergeRound(h64, v1);
            h64 = mergeRound(h64, v2);
            h64 = mergeRound(h64, v3);
            h64 = mergeRound(h64, v4);
        } else {
            h64 = seed + PRIME5;
        }
        h64 += totalLen;

        int idx = 0;
        while (idx + 8 <= bufferSize) {
            long k1 = round(0, getLong(buffer, idx));
            h64 ^= k1;
            h64 = Long.rotateLeft(h64, 27) * PRIME1 + PRIME4;
            idx += 8;
        }

        if (idx + 4 <= bufferSize) {
            h64 ^= (getInt(buffer, idx) & 0xFFFFFFFFL) * PRIME1;
            h64 = Long.rotateLeft(h64, 23) * PRIME2 + PRIME3;
            idx += 4;
        }

        while (idx < bufferSize) {
            h64 ^= (buffer[idx] & 0xFF) * PRIME5;
            h64 = Long.rotateLeft(h64, 11) * PRIME1;
            idx++;
        }

        h64 ^= h64 >>> 33;
        h64 *= PRIME2;
        h64 ^= h64 >>> 29;
        h64 *= PRIME3;
        h64 ^= h64 >>> 32;
        return h64;
    }

    private static long round(long acc, long input) {
        acc += input * PRIME2;
        acc = Long.rotateLeft(acc, 31);
        acc *= PRIME1;
        return acc;
    }

    private static long mergeRound(long acc, long val) {
        val = round(0, val);
        acc ^= val;
        acc = acc * PRIME1 + PRIME4;
        return acc;
    }

    private static long getLong(byte[] data, int offset) {
        return ((long) data[offset] & 0xFF)
                | (((long) data[offset + 1] & 0xFF) << 8)
                | (((long) data[offset + 2] & 0xFF) << 16)
                | (((long) data[offset + 3] & 0xFF) << 24)
                | (((long) data[offset + 4] & 0xFF) << 32)
                | (((long) data[offset + 5] & 0xFF) << 40)
                | (((long) data[offset + 6] & 0xFF) << 48)
                | (((long) data[offset + 7] & 0xFF) << 56);
    }

    private static int getInt(byte[] data, int offset) {
        return (data[offset] & 0xFF)
                | ((data[offset + 1] & 0xFF) << 8)
                | ((data[offset + 2] & 0xFF) << 16)
                | ((data[offset + 3] & 0xFF) << 24);
    }
}
