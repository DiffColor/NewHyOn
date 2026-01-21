package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmList;
import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

public class RealmElement extends RealmObject {

    @PrimaryKey
    private String elementId;
    private String pageId;
    private String name;
    private String type;
    private double width;
    private double height;
    private double posTop;
    private double posLeft;
    private int zIndex;
    private RealmList<RealmContent> contents;

    public String getElementId() {
        return elementId;
    }

    public void setElementId(String elementId) {
        this.elementId = elementId;
    }

    public String getPageId() {
        return pageId;
    }

    public void setPageId(String pageId) {
        this.pageId = pageId;
    }

    public String getName() {
        return name;
    }

    public void setName(String name) {
        this.name = name;
    }

    public String getType() {
        return type;
    }

    public void setType(String type) {
        this.type = type;
    }

    public double getWidth() {
        return width;
    }

    public void setWidth(double width) {
        this.width = width;
    }

    public double getHeight() {
        return height;
    }

    public void setHeight(double height) {
        this.height = height;
    }

    public double getPosTop() {
        return posTop;
    }

    public void setPosTop(double posTop) {
        this.posTop = posTop;
    }

    public double getPosLeft() {
        return posLeft;
    }

    public void setPosLeft(double posLeft) {
        this.posLeft = posLeft;
    }

    public int getzIndex() {
        return zIndex;
    }

    public void setzIndex(int zIndex) {
        this.zIndex = zIndex;
    }

    public RealmList<RealmContent> getContents() {
        return contents;
    }

    public void setContents(RealmList<RealmContent> contents) {
        this.contents = contents;
    }
}
