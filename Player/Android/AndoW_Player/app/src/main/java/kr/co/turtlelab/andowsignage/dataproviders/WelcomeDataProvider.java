package kr.co.turtlelab.andowsignage.dataproviders;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.data.realm.RealmElement;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
import kr.co.turtlelab.andowsignage.data.realm.RealmWelcome;
import kr.co.turtlelab.andowsignage.datamodels.WelcomeDataModel;

public class WelcomeDataProvider {

    private WelcomeDataProvider() {
    }

    public static WelcomeDataModel getContent(String pageName, String elementName) {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPage page = realm.where(RealmPage.class)
                    .equalTo("pageName", pageName)
                    .findFirst();
            if (page == null) {
                return null;
            }
            RealmPage detached = realm.copyFromRealm(page);
            RealmElement element = null;
            if (detached.getElements() != null) {
                for (RealmElement e : detached.getElements()) {
                    if (elementName.equalsIgnoreCase(e.getName())) {
                        element = e;
                        break;
                    }
                }
            }
            if (element == null) {
                return null;
            }
            String elementId = detached.getPageId() + "_" + element.getName();
            RealmWelcome welcome = realm.where(RealmWelcome.class)
                    .equalTo("elementId", elementId)
                    .findFirst();
            if (welcome == null) {
                return null;
            }
            WelcomeDataModel model = new WelcomeDataModel();
            model.setLocalImage(welcome.getImageFileName(), welcome.getImageFilePath());
            return model;
        } finally {
            realm.close();
        }
    }
}
