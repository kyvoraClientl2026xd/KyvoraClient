package com.kyvora.hud;

import java.io.File;
import net.minecraft.client.Minecraft;
import net.minecraftforge.common.config.Configuration;
import org.lwjgl.input.Keyboard;

public final class HudConfig {
    public static boolean showKeystrokes = false;
    public static boolean showCps = false;
    public static boolean showFps = false;
    public static boolean showCoordinates = false;
    public static boolean showPing = false;
    public static boolean showDirection = false;
    public static boolean showBiome = false;
    public static boolean showClock = false;
    public static boolean showSpeed = false;
    public static boolean showHealth = false;
    public static boolean showArmor = false;
    public static boolean showSprint = false;
    public static boolean showTarget = false;
    public static boolean showShiftKey = true;
    public static int menuKeyCode = Keyboard.KEY_RSHIFT;
    public static int hudScalePercent = 100;
    public static boolean showKyvoraTag = false;
    public static int keystrokesX = 8;
    public static int keystrokesY = 120;
    public static int infoX = 8;
    public static int infoY = 8;
    public static boolean editingPositions = false;

    private static Configuration configuration;

    private HudConfig() {
    }

    public static void load() {
        File configFile = new File(
                Minecraft.getMinecraft().mcDataDir,
                "config/kyvora-hud.cfg"
        );
        configuration = new Configuration(configFile);
        configuration.load();
        boolean applyOptInDefaults = configuration.get("hud", "modsMenuVersion", 0).getInt() < 1;

        showKeystrokes = configuration.getBoolean(
                "keystrokes", "hud", false, "Show the WASD and mouse key display."
        );
        showCps = configuration.getBoolean(
                "cps", "hud", false, "Show left and right clicks per second."
        );
        showFps = configuration.getBoolean(
                "fps", "hud", false, "Show frames per second."
        );
        showCoordinates = configuration.getBoolean(
                "coordinates", "hud", false, "Show player coordinates."
        );
        showPing = configuration.getBoolean(
                "ping", "hud", false, "Show the current server ping."
        );
        showDirection = configuration.getBoolean("direction", "hud", false, "Show compass direction.");
        showBiome = configuration.getBoolean("biome", "hud", false, "Show current biome.");
        showClock = configuration.getBoolean("clock", "hud", false, "Show in-game time.");
        showSpeed = configuration.getBoolean("speed", "hud", false, "Show horizontal movement speed.");
        showHealth = configuration.getBoolean("health", "hud", false, "Show health and hunger.");
        showArmor = configuration.getBoolean("armor", "hud", false, "Show armor points.");
        showSprint = configuration.getBoolean("sprint", "hud", false, "Show sprint state.");
        showTarget = configuration.getBoolean("target", "hud", false, "Show crosshair target details.");
        showShiftKey = configuration.getBoolean("showShiftKey", "hud", true, "Show Shift in the keystrokes panel.");
        menuKeyCode = configuration.getInt(
                "menuKeyCode", "controls", Keyboard.KEY_RSHIFT, 0, 255, "Open the Kyvora mod menu."
        );
        hudScalePercent = configuration.getInt(
                "hudScalePercent", "hud", 100, 50, 150, "Scale Kyvora HUD widgets from 50% to 150%."
        );
        showKyvoraTag = configuration.getBoolean(
                "showKyvoraTag", "hud", false, "Show the Kyvora K badge above your player in third person."
        );
        if (applyOptInDefaults) {
            showKeystrokes = false;
            showCps = false;
            showFps = false;
            showCoordinates = false;
            showPing = false;
            showDirection = false;
            showBiome = false;
            showClock = false;
            showSpeed = false;
            showHealth = false;
            showArmor = false;
            showSprint = false;
            showTarget = false;
            showKyvoraTag = false;
            configuration.get("hud", "modsMenuVersion", 0).set(1);
        }
        keystrokesX = configuration.getInt("keystrokesX", "positions", 8, 0, 10000, "Keystrokes panel X position.");
        keystrokesY = configuration.getInt("keystrokesY", "positions", 120, 0, 10000, "Keystrokes panel Y position.");
        infoX = configuration.getInt("infoX", "positions", 8, 0, 10000, "Stats panel X position.");
        infoY = configuration.getInt("infoY", "positions", 8, 0, 10000, "Stats panel Y position.");
        save();
    }

    public static void save() {
        if (configuration == null) {
            return;
        }

        configuration.get("hud", "keystrokes", false).set(showKeystrokes);
        configuration.get("hud", "cps", false).set(showCps);
        configuration.get("hud", "fps", false).set(showFps);
        configuration.get("hud", "coordinates", false).set(showCoordinates);
        configuration.get("hud", "ping", false).set(showPing);
        configuration.get("hud", "direction", false).set(showDirection);
        configuration.get("hud", "biome", false).set(showBiome);
        configuration.get("hud", "clock", false).set(showClock);
        configuration.get("hud", "speed", false).set(showSpeed);
        configuration.get("hud", "health", false).set(showHealth);
        configuration.get("hud", "armor", false).set(showArmor);
        configuration.get("hud", "sprint", false).set(showSprint);
        configuration.get("hud", "target", false).set(showTarget);
        configuration.get("hud", "showShiftKey", true).set(showShiftKey);
        configuration.get("controls", "menuKeyCode", Keyboard.KEY_RSHIFT).set(menuKeyCode);
        configuration.get("hud", "hudScalePercent", 100, "Scale Kyvora HUD widgets.", 50, 150).set(hudScalePercent);
        configuration.get("hud", "showKyvoraTag", false).set(showKyvoraTag);
        configuration.get("positions", "keystrokesX", 8, "Keystrokes panel X position.", 0, 10000).set(keystrokesX);
        configuration.get("positions", "keystrokesY", 120, "Keystrokes panel Y position.", 0, 10000).set(keystrokesY);
        configuration.get("positions", "infoX", 8, "Stats panel X position.", 0, 10000).set(infoX);
        configuration.get("positions", "infoY", 8, "Stats panel Y position.", 0, 10000).set(infoY);
        configuration.save();
    }
}
