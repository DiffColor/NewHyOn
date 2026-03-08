package kr.co.turtlelab.andowsignage.data;

import android.text.TextUtils;

import kr.co.turtlelab.andowsignage.data.rethink.RethinkDbClient;
import kr.co.turtlelab.andowsignage.data.rethink.RethinkModels;
import kr.co.turtlelab.andowsignage.dataproviders.LocalSettingsProvider;

/**
 * manager ip 로 RethinkDB(ServerSettings)를 조회해 단말 통신 설정을 동기화한다.
 */
public final class CommunicationSettingsSync {

    private CommunicationSettingsSync() {
    }

    public static boolean syncFromServerAndApply(String bootstrapHost) {
        String host = normalize(bootstrapHost);
        if (!TextUtils.isEmpty(host)) {
            RethinkDbClient.getInstance().updateHost(host);
        }

        boolean synced = false;
        String resolvedDataServerHost = "";
        RethinkModels.ServerSettingsRecord record = RethinkDbClient.getInstance().fetchServerSettings();
        if (record != null) {
            resolvedDataServerHost = normalize(record.getDataServerIp());
            LocalSettingsProvider.updateCommunicationSettings(
                    resolvedDataServerHost,
                    normalize(record.getMessageServerIp()),
                    record.getFtpPort(),
                    record.getFtpPasvMinPort(),
                    record.getFtpPasvMaxPort(),
                    normalize(record.getFtpRootPath()));
            synced = true;
        }

        LocalSettingsProvider.applyStoredCommunicationSettings();

        if (!TextUtils.isEmpty(resolvedDataServerHost)) {
            RethinkDbClient.getInstance().updateHost(resolvedDataServerHost);
        } else if (!TextUtils.isEmpty(host)) {
            RethinkDbClient.getInstance().updateHost(host);
        }
        return synced;
    }

    private static String normalize(String value) {
        if (TextUtils.isEmpty(value)) {
            return "";
        }
        return value.trim();
    }
}
