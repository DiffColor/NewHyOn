package kr.co.turtlelab.andowsignage.data.update;

public final class UpdateThrottleModels {

    private UpdateThrottleModels() { }

    public static final class UpdateThrottleSettings {
        public String Id = "global";
        public int MaxConcurrentDownloads = 8;
        public int RetryIntervalSeconds = 60;
        public int LeaseTtlSeconds = 3600;
        public int LeaseRenewIntervalSeconds = 30;
        public int SettingsRefreshSeconds = 1800;
        public String UpdatedAt = "";
    }

    public static final class UpdateLeaseEntry {
        public String Id;
        public String PlayerId;
        public String QueueId;
        public String LastRenewAt;
    }
}
