package kr.co.turtlelab.quber.ntpsettings;

import java.util.regex.Pattern;

final class NtpServerValidator {
    private static final Pattern LABEL = Pattern.compile("[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?");

    private NtpServerValidator() {
    }

    static boolean isValid(String value) {
        if (value == null) return false;
        String server = value.trim();
        if (server.isEmpty() || server.length() > 253) return false;
        if (server.contains("://") || server.contains(":") || server.contains("/") || server.contains(" ")) {
            return false;
        }
        if (isIpv4(server)) return true;
        if (containsOnlyDigitsAndDots(server)) return false;

        String[] labels = server.split("\\.", -1);
        if (labels.length < 2) return false;
        for (String label : labels) {
            if (!LABEL.matcher(label).matches()) return false;
        }
        return true;
    }

    private static boolean containsOnlyDigitsAndDots(String value) {
        for (int i = 0; i < value.length(); i++) {
            char character = value.charAt(i);
            if (!Character.isDigit(character) && character != '.') return false;
        }
        return true;
    }

    private static boolean isIpv4(String value) {
        String[] parts = value.split("\\.", -1);
        if (parts.length != 4) return false;
        for (String part : parts) {
            if (part.isEmpty() || part.length() > 3) return false;
            for (int i = 0; i < part.length(); i++) {
                if (!Character.isDigit(part.charAt(i))) return false;
            }
            int number;
            try {
                number = Integer.parseInt(part);
            } catch (NumberFormatException ignored) {
                return false;
            }
            if (number < 0 || number > 255) return false;
        }
        return true;
    }
}
