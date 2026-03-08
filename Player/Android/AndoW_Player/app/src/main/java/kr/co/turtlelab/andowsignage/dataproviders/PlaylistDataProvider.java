package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import io.realm.Sort;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
import kr.co.turtlelab.andowsignage.datamodels.PageDataModel;

public class PlaylistDataProvider {

    private PlaylistDataProvider() {
    }

    public static List<PageDataModel> getPageList(String playlistName) {
        List<PageDataModel> pageList = new ArrayList<>();
        if (playlistName == null || playlistName.isEmpty()) {
            return pageList;
        }
        Realm realm = Realm.getDefaultInstance();
        try {
            List<RealmPage> realmPages = realm.copyFromRealm(
                    realm.where(RealmPage.class)
                            .equalTo("playlistName", playlistName)
                            .sort("orderIndex", Sort.ASCENDING)
                            .findAll());
            for (RealmPage realmPage : realmPages) {
                PageDataModel pdm = new PageDataModel();
                pdm.setPageName(realmPage.getPageName());
                pdm.setPlayTime(String.valueOf(realmPage.getPlayHour()),
                        String.valueOf(realmPage.getPlayMinute()),
                        String.valueOf(realmPage.getPlaySecond()));
                pdm.setVolume(String.valueOf(realmPage.getVolume()));
                pdm.setGUID(realmPage.getPageId());
                pageList.add(pdm);
            }
        } finally {
            realm.close();
        }
        return pageList;
    }
}
