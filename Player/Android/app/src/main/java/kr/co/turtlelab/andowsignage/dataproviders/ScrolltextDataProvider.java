package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.data.realm.RealmContent;
import kr.co.turtlelab.andowsignage.data.realm.RealmElement;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
import kr.co.turtlelab.andowsignage.datamodels.ScrolltextDataModel;

public class ScrolltextDataProvider {

    private ScrolltextDataProvider() {
    }

    public static List<ScrolltextDataModel> getContentList(String pageName, String elementName) {
        List<ScrolltextDataModel> contentList = new ArrayList<>();
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
                ScrolltextDataModel sdm = new ScrolltextDataModel();
                sdm.setText(realmContent.getFileName());
                sdm.setFont(realmContent.getContentType());
                sdm.setBackColor(realmContent.getPlaySecond());
                sdm.setForeColor(realmContent.getPlayMinute());
                sdm.setScrolltime(String.valueOf(Math.max(1, realmContent.getScrollSpeedSec())));
                contentList.add(sdm);
            }
        } finally {
            realm.close();
        }
        return contentList;
    }
}
