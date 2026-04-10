package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmElement;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
import kr.co.turtlelab.andowsignage.datamodels.ElementDataModel;

public class ElementDataProvider {

    private ElementDataProvider() {
    }

    public static List<ElementDataModel> getPageElementList(String pageId) {
        List<ElementDataModel> elementList = new ArrayList<>();
        if (pageId == null || pageId.isEmpty()) {
            return elementList;
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPage realmPage = realm.where(RealmPage.class)
                    .equalTo("pageId", pageId)
                    .findFirst();
            if (realmPage == null || realmPage.getElements() == null) {
                return elementList;
            }
            RealmPage detached = realm.copyFromRealm(realmPage);
            float[] scales = AndoWSignageApp.getScaleFactorsForCanvas(detached.getCanvasWidth(), detached.getCanvasHeight());
            List<RealmElement> realmElements = detached.getElements();
            Collections.sort(realmElements, new Comparator<RealmElement>() {
                @Override
                public int compare(RealmElement o1, RealmElement o2) {
                    return o1.getzIndex() - o2.getzIndex();
                }
            });
            for (RealmElement realmElement : realmElements) {
                ElementDataModel evm = new ElementDataModel();
                evm.setScales(scales[0], scales[1], scales[2]);
                evm.setid(String.valueOf(elementList.size()));
                evm.setName(realmElement.getName());
                evm.setType(realmElement.getType());
                evm.setWidth(String.valueOf(realmElement.getWidth()));
                evm.setHeight(String.valueOf(realmElement.getHeight()));
                evm.setX(String.valueOf(realmElement.getPosLeft()));
                evm.setY(String.valueOf(realmElement.getPosTop()));
                evm.setZ(String.valueOf(realmElement.getzIndex()));
                elementList.add(evm);
            }
        } finally {
            realm.close();
        }
        return elementList;
    }
}
