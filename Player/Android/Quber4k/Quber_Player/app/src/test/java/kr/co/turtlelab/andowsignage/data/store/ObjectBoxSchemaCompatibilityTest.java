package kr.co.turtlelab.andowsignage.data.store;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class ObjectBoxSchemaCompatibilityTest {

    @Test
    public void pifFingerprintKeepsProductionPropertyId() {
        assertEquals(18, StoredLocalSettings_.pifFingerprint.id);
    }

    @Test
    public void pifFingerprintRoundTripsOnStoredSettings() {
        StoredLocalSettings settings = new StoredLocalSettings();

        settings.setPifFingerprint("device-fingerprint");

        assertEquals("device-fingerprint", settings.getPifFingerprint());
    }
}
