package kr.co.turtlelab.andowsignage.data.update;

import java.io.File;
import java.io.FileWriter;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

/**
 * 간단한 큐 상태 기록용 로거.
 */
public final class UpdateQueueLogger {

    private static final String LOG_DIR = "Logs";
    private static final String LOG_FILE = "queue.log";
    private static final Object LOCK = new Object();

    private UpdateQueueLogger() { }

    public static void log(String message) {
        try {
            File dir = new File(LocalPathUtils.getAbsolutePath(LOG_DIR));
            if (!dir.exists()) {
                dir.mkdirs();
            }
            File file = new File(dir, LOG_FILE);
            String timeStamp = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.KOREA)
                    .format(new Date());
            synchronized (LOCK) {
                try (FileWriter writer = new FileWriter(file, true)) {
                    writer.write(timeStamp + "  " + message + "\n");
                }
            }
        } catch (Exception ignored) {
        }
    }
}
