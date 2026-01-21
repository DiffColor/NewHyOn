package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

/**
 * 업데이트 세트(대기열) 상태를 보관하는 Realm 모델.
 * 실제 플레이어 데이터(페이지/요소/콘텐츠)는 기존 스키마를 유지하고,
 * 이 테이블에 JSON 블롭과 진행 상태만 관리한다.
 */
public class RealmUpdateQueue extends RealmObject {

    @PrimaryKey
    private long id;
    private String type;
    private String payloadJson;
    private String downloadContentsJson;
    private String status;
    private float progress;
    private float downloadProgress;
    private float validateProgress;
    private String errorCode;
    private String errorMessage;
    private String externalId;
    private long createdAt;
    private long updatedAt;
    private long expiresAt;
    private int retryCount;
    private long nextRetryAt;

    public long getId() {
        return id;
    }

    public void setId(long id) {
        this.id = id;
    }

    public String getType() {
        return type;
    }

    public void setType(String type) {
        this.type = type;
    }

    public String getPayloadJson() {
        return payloadJson;
    }

    public void setPayloadJson(String payloadJson) {
        this.payloadJson = payloadJson;
    }

    public String getDownloadContentsJson() {
        return downloadContentsJson;
    }

    public void setDownloadContentsJson(String downloadContentsJson) {
        this.downloadContentsJson = downloadContentsJson;
    }

    public String getStatus() {
        return status;
    }

    public void setStatus(String status) {
        this.status = status;
    }

    public float getProgress() {
        return progress;
    }

    public void setProgress(float progress) {
        this.progress = progress;
    }

    public float getDownloadProgress() {
        return downloadProgress;
    }

    public void setDownloadProgress(float downloadProgress) {
        this.downloadProgress = downloadProgress;
    }

    public float getValidateProgress() {
        return validateProgress;
    }

    public void setValidateProgress(float validateProgress) {
        this.validateProgress = validateProgress;
    }

    public String getErrorCode() {
        return errorCode;
    }

    public void setErrorCode(String errorCode) {
        this.errorCode = errorCode;
    }

    public String getErrorMessage() {
        return errorMessage;
    }

    public void setErrorMessage(String errorMessage) {
        this.errorMessage = errorMessage;
    }

    public String getExternalId() {
        return externalId;
    }

    public void setExternalId(String externalId) {
        this.externalId = externalId;
    }

    public boolean isReady() {
        return "ready".equalsIgnoreCase(status);
    }

    public long getCreatedAt() {
        return createdAt;
    }

    public void setCreatedAt(long createdAt) {
        this.createdAt = createdAt;
    }

    public long getUpdatedAt() {
        return updatedAt;
    }

    public void setUpdatedAt(long updatedAt) {
        this.updatedAt = updatedAt;
    }

    public long getExpiresAt() {
        return expiresAt;
    }

    public void setExpiresAt(long expiresAt) {
        this.expiresAt = expiresAt;
    }

    public int getRetryCount() {
        return retryCount;
    }

    public void setRetryCount(int retryCount) {
        this.retryCount = retryCount;
    }

    public long getNextRetryAt() {
        return nextRetryAt;
    }

    public void setNextRetryAt(long nextRetryAt) {
        this.nextRetryAt = nextRetryAt;
    }
}
