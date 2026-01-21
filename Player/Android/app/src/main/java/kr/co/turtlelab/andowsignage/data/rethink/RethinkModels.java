package kr.co.turtlelab.andowsignage.data.rethink;

import com.google.gson.annotations.SerializedName;

import java.util.ArrayList;
import java.util.List;

/**
 * DTO 모델들은 RethinkDB 에 저장된 AndoW Manager 의 스키마와 1:1 로 매핑된다.
 * Realm 저장 시에는 필요한 필드만 추려서 사용한다.
 */
public final class RethinkModels {

    private RethinkModels() {
    }

    public static class PlayerInfoRecord {
        @SerializedName("id")
        private String guid;
        @SerializedName("PIF_PlayerName")
        private String playerName;
        @SerializedName("PIF_CurrentPlayList")
        private String playlist;
        @SerializedName("PIF_IsLandScape")
        private boolean landscape;
        @SerializedName("PIF_IPAddress")
        private String ipAddress;
        @SerializedName("command")
        private String command;

        public String getGuid() {
            return guid;
        }

        public String getPlayerName() {
            return playerName;
        }

        public String getPlaylist() {
            return playlist;
        }

        public boolean isLandscape() {
            return landscape;
        }

        public String getIpAddress() {
            return ipAddress;
        }

        public String getCommand() {
            return command;
        }
    }

    public static class PageListRecord {
        @SerializedName("id")
        private String id;
        @SerializedName("PLI_PageListName")
        private String name;
        @SerializedName("PLI_PageDirection")
        private String direction;
        @SerializedName("PLI_Pages")
        private List<String> pages = new ArrayList<>();

        public String getName() {
            return name;
        }

        public List<String> getPages() {
            return pages;
        }

        public String getDirection() {
            return direction;
        }

        public String getId() {
            return id;
        }
    }

    public static class PageInfoRecord {
        @SerializedName("id")
        private String guid;
        @SerializedName("PIC_PageName")
        private String pageName;
        @SerializedName("PIC_PlaytimeHour")
        private int playHour;
        @SerializedName("PIC_PlaytimeMinute")
        private int playMinute;
        @SerializedName("PIC_PlaytimeSecond")
        private int playSecond;
        @SerializedName("PIC_Volume")
        private int volume;
        @SerializedName("PIC_IsLandscape")
        private boolean landscape;
        @SerializedName("PIC_Elements")
        private List<ElementInfoRecord> elements = new ArrayList<>();

        public String getGuid() {
            return guid;
        }

        public String getPageName() {
            return pageName;
        }

        public int getPlayHour() {
            return playHour;
        }

        public int getPlayMinute() {
            return playMinute;
        }

        public int getPlaySecond() {
            return playSecond;
        }

        public int getVolume() {
            return volume;
        }

        public boolean isLandscape() {
            return landscape;
        }

        public List<ElementInfoRecord> getElements() {
            return elements;
        }
    }

    public static class ElementInfoRecord {
        @SerializedName("EIF_Name")
        private String name;
        @SerializedName("EIF_Type")
        private String type;
        @SerializedName("EIF_Width")
        private double width;
        @SerializedName("EIF_Height")
        private double height;
        @SerializedName("EIF_PosTop")
        private double posTop;
        @SerializedName("EIF_PosLeft")
        private double posLeft;
        @SerializedName("EIF_ZIndex")
        private int zIndex;
        @SerializedName("EIF_DataFileName")
        private String dataFileName;
        @SerializedName("EIF_DataFileFullPath")
        private String dataFileFullPath;
        @SerializedName("EIF_ContentsInfoClassList")
        private List<ContentInfoRecord> contents = new ArrayList<>();

        public String getName() {
            return name;
        }

        public String getType() {
            return type;
        }

        public double getWidth() {
            return width;
        }

        public double getHeight() {
            return height;
        }

        public double getPosTop() {
            return posTop;
        }

        public double getPosLeft() {
            return posLeft;
        }

        public int getzIndex() {
            return zIndex;
        }

        public String getDataFileName() {
            return dataFileName;
        }

        public String getDataFileFullPath() {
            return dataFileFullPath;
        }

        public List<ContentInfoRecord> getContents() {
            return contents;
        }
    }

    public static class ContentInfoRecord {
        @SerializedName("CIF_FileName")
        private String fileName;
        @SerializedName("CIF_FileFullPath")
        private String fileFullPath;
        @SerializedName("CIF_RelativePath")
        private String fileRelativePath;
        @SerializedName("CIF_FileExist")
        private boolean fileExist;
        @SerializedName("CIF_PlayMinute")
        private String playMinute;
        @SerializedName("CIF_PlaySec")
        private String playSecond;
        @SerializedName("CIF_ContentType")
        private String contentType;
        @SerializedName("CIF_ValidTime")
        private boolean validTime;
        @SerializedName("CIF_ScrollTextSpeedSec")
        private int scrollSpeedSec;
        @SerializedName("CIF_FileStrGUID")
        private String guid;
        @SerializedName("CIF_FileSize")
        private long fileSize;
        @SerializedName("CIF_FileHash")
        private String fileHash;

        public String getFileName() {
            return fileName;
        }

        public String getFileFullPath() {
            return fileFullPath;
        }

        public String getRelativePath() {
            return fileRelativePath;
        }

        public boolean isFileExist() {
            return fileExist;
        }

        public String getPlayMinute() {
            return playMinute;
        }

        public String getPlaySecond() {
            return playSecond;
        }

        public String getContentType() {
            return contentType;
        }

        public boolean isValidTime() {
            return validTime;
        }

        public int getScrollSpeedSec() {
            return scrollSpeedSec;
        }

        public String getGuid() {
            return guid;
        }

        public long getFileSize() {
            return fileSize;
        }

        public String getFileHash() {
            return fileHash;
        }
    }

    public static class TextInfoRecord {
        @SerializedName("CIF_PageName")
        private String pageName;
        @SerializedName("CIF_DataFileName")
        private String dataFileName;
        @SerializedName("CIF_DataImageFileName")
        private String imageFileName;
        @SerializedName("CIF_BGImageFileFullPath")
        private String bgImageFullPath;

        public String getPageName() {
            return pageName;
        }

        public String getDataFileName() {
            return dataFileName;
        }

        public String getImageFileName() {
            return imageFileName;
        }

        public String getBgImageFullPath() {
            return bgImageFullPath;
        }
    }

    public static class WeeklyScheduleRecord {
        @SerializedName("id")
        private String id;
        @SerializedName("PlayerID")
        private String playerId;
        @SerializedName("PlayerName")
        private String playerName;
        @SerializedName("MonSch")
        private DayScheduleRecord monday;
        @SerializedName("TueSch")
        private DayScheduleRecord tuesday;
        @SerializedName("WedSch")
        private DayScheduleRecord wednesday;
        @SerializedName("ThuSch")
        private DayScheduleRecord thursday;
        @SerializedName("FriSch")
        private DayScheduleRecord friday;
        @SerializedName("SatSch")
        private DayScheduleRecord saturday;
        @SerializedName("SunSch")
        private DayScheduleRecord sunday;

        public String getId() {
            return id;
        }

        public String getPlayerId() {
            return playerId;
        }

        public DayScheduleRecord getMonday() {
            return monday;
        }

        public DayScheduleRecord getTuesday() {
            return tuesday;
        }

        public DayScheduleRecord getWednesday() {
            return wednesday;
        }

        public DayScheduleRecord getThursday() {
            return thursday;
        }

        public DayScheduleRecord getFriday() {
            return friday;
        }

        public DayScheduleRecord getSaturday() {
            return saturday;
        }

        public DayScheduleRecord getSunday() {
            return sunday;
        }
    }

    public static class DayScheduleRecord {
        @SerializedName("StartHour")
        private int startHour;
        @SerializedName("StartMinute")
        private int startMinute;
        @SerializedName("EndHour")
        private int endHour;
        @SerializedName("EndMinute")
        private int endMinute;
        @SerializedName("IsOnAir")
        private Boolean onAir;

        public int getStartHour() {
            return startHour;
        }

        public int getStartMinute() {
            return startMinute;
        }

        public int getEndHour() {
            return endHour;
        }

        public int getEndMinute() {
            return endMinute;
        }

        public boolean isOnAir() {
            return onAir == null || onAir;
        }
    }
}
