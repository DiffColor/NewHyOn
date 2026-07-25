package kr.co.turtlelab.andowsignage.data.update;

import java.util.Locale;

/** 업데이트 오류를 영구 오류와 일시 오류로 구분하고 재시도 상한을 결정한다. */
public final class UpdateFailurePolicy {

    public static final int MAX_ATTEMPTS = 3;

    private static final String[] PERMANENT_ERROR_CODES = {
            "MISSING_FILE",
            "REMOTE_PATH_EMPTY",
            "LOCAL_FILENAME_EMPTY",
            "MOVE_MISSING",
            "INVALID_PAYLOAD",
            "INVALID_SCHEDULE_PAYLOAD"
    };

    private UpdateFailurePolicy() { }

    public static boolean shouldRetry(String errorMessage, int attemptNumber) {
        return attemptNumber < MAX_ATTEMPTS && !isPermanent(errorMessage);
    }

    public static boolean isPermanent(String errorMessage) {
        return !getPermanentErrorCode(errorMessage).isEmpty();
    }

    public static String getFinalErrorCode(String errorMessage, int attemptNumber) {
        return getFinalErrorCode(errorMessage, attemptNumber, "DOWNLOAD_RETRY_EXHAUSTED");
    }

    public static String getFinalErrorCode(String errorMessage, int attemptNumber, String exhaustedErrorCode) {
        String permanentCode = getPermanentErrorCode(errorMessage);
        if (!permanentCode.isEmpty()) {
            return permanentCode;
        }
        return attemptNumber >= MAX_ATTEMPTS ? exhaustedErrorCode : "UPDATE";
    }

    public static String getPermanentErrorCode(String errorMessage) {
        String normalized = errorMessage == null ? "" : errorMessage.trim().toUpperCase(Locale.US);
        for (String code : PERMANENT_ERROR_CODES) {
            if (normalized.contains(code)) {
                return code;
            }
        }
        return "";
    }
}
