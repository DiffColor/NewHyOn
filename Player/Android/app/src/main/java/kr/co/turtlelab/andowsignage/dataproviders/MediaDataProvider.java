package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.data.realm.RealmContent;
import kr.co.turtlelab.andowsignage.data.realm.RealmElement;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;

public class MediaDataProvider {

    private MediaDataProvider() {
    }

    public static List<MediaDataModel> getContentList(String pageName, String elementName) {
        List<MediaDataModel> contentList = new ArrayList<>();
        if (pageName == null || elementName == null) {
            return contentList;
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPage page = realm.where(RealmPage.class)
                    .equalTo("pageName", pageName)
                    .findFirst();
            if (page == null || page.getElements() == null) {
                return contentList;
            }
            RealmPage detached = realm.copyFromRealm(page);
            RealmElement target = null;
            for (RealmElement element : detached.getElements()) {
                if (elementName.equalsIgnoreCase(element.getName())) {
                    target = element;
                    break;
                }
            }
            if (target == null || target.getContents() == null) {
                return contentList;
            }
            for (RealmContent realmContent : target.getContents()) {
                MediaDataModel mdm = new MediaDataModel();
                if (realmContent.getFileFullPath() != null && !realmContent.getFileFullPath().isEmpty()) {
                    mdm.setFilePath(realmContent.getFileFullPath());
                } else if (realmContent.getFileName() != null) {
                    mdm.setFileName(realmContent.getFileName());
                }
                mdm.setType(realmContent.getContentType());
                mdm.setPlayTime(realmContent.getPlayMinute(), realmContent.getPlaySecond());
                mdm.setValidState(String.valueOf(realmContent.isContentValid()));
                contentList.add(mdm);
            }
        } finally {
            realm.close();
        }
        return contentList;
    }
}
