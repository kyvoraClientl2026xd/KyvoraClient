package com.kyvora.hud;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.client.gui.ScaledResolution;
import org.lwjgl.input.Keyboard;
import org.lwjgl.input.Mouse;

/** Kyvora's in-game mod menu, opened with right Shift. */
public final class HudSettingsScreen extends GuiScreen {
    private static final int CARD_HEIGHT = 122;
    private static final int CARD_GAP = 10;
    private static final int HEADER_HEIGHT = 53;
    private static final String[] FILTERS = {"ALL", "HUD", "PVP", "UTILITY"};
    private static final ModEntry[] MODS = {
            new ModEntry("keystrokes", "Keystrokes", "WASD, espacio, Shift y clics", "HUD", "KEY"),
            new ModEntry("cps", "CPS", "Clics por segundo: izquierdo / derecho", "PVP", "CPS"),
            new ModEntry("fps", "FPS", "Muestra tus fotogramas por segundo", "HUD", "FPS"),
            new ModEntry("coordinates", "Coordinates", "Posición actual en el mundo", "HUD", "XYZ"),
            new ModEntry("ping", "Ping", "Latencia de tu conexión", "PVP", "MS"),
            new ModEntry("kyvora", "Kyvora Badge", "Insignia K morada sobre tu personaje", "UTILITY", "K"),
            new ModEntry("direction", "Direction", "Rumbo de la brújula", "HUD", "DIR"),
            new ModEntry("biome", "Biome", "Nombre del bioma actual", "HUD", "BIO"),
            new ModEntry("clock", "World Clock", "Hora aproximada del mundo", "HUD", "TIME"),
            new ModEntry("speed", "Speed", "Velocidad horizontal del jugador", "PVP", "SPD"),
            new ModEntry("health", "Health & Hunger", "Vida y nivel de hambre", "HUD", "HP"),
            new ModEntry("armor", "Armor Status", "Puntos de armadura equipados", "HUD", "ARM"),
            new ModEntry("sprint", "Sprint Status", "Indica cuándo estás corriendo", "PVP", "RUN"),
            new ModEntry("target", "Target Info", "Vida de la entidad bajo la mira", "PVP", "TGT")
    };

    private String selectedFilter = "ALL";
    private String searchText = "";
    private boolean searchFocused;
    private boolean optionsView;
    private boolean globalSettings;
    private boolean waitingForMenuKey;
    private boolean draggingScale;
    private int selectedMod;
    private int scrollRow;

    @Override
    public void initGui() {
        Keyboard.enableRepeatEvents(true);
    }

    @Override
    public void onGuiClosed() {
        Keyboard.enableRepeatEvents(false);
        waitingForMenuKey = false;
        super.onGuiClosed();
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        drawDefaultBackground();
        Layout layout = new Layout(width, height);
        Gui.drawRect(0, 0, width, height, 0x7204070D);
        drawPanel(layout);
        drawHeader(layout);
        if (globalSettings) drawGlobalSettings(layout);
        else if (optionsView) drawOptions(layout);
        else drawModBrowser(layout, mouseX, mouseY);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private void drawPanel(Layout l) {
        Gui.drawRect(l.x - 1, l.y - 1, l.x + l.w + 1, l.y + l.h + 1, 0xB87D50CC);
        Gui.drawRect(l.x, l.y, l.x + l.w, l.y + l.h, 0xE912141B);
        Gui.drawRect(l.x, l.y + HEADER_HEIGHT, l.x + l.w, l.y + HEADER_HEIGHT + 1, 0xFF38323F);
    }

    private void drawHeader(Layout l) {
        Gui.drawRect(l.x + 16, l.y + 12, l.x + 48, l.y + 44, 0xFF9E65F3);
        Gui.drawRect(l.x + 19, l.y + 15, l.x + 45, l.y + 41, 0xFF21172D);
        drawCenteredString(fontRendererObj, "K", l.x + 32, l.y + 20, 0xFFEAD8FF);
        fontRendererObj.drawString("KYVORA", l.x + 58, l.y + 15, 0xFFF5F1FA);
        fontRendererObj.drawString("CLIENT", l.x + 112, l.y + 15, 0xFFB8B3C0);
        fontRendererObj.drawString(optionsView ? "MOD OPTIONS" : "MODS", l.x + 58, l.y + 32, 0xFFAE92D9);
        drawAction(l.x + l.w - 38, l.y + 14, 24, 24, "X", 0xFF3B303F);
        if (!optionsView && !globalSettings) {
            drawAction(l.x + l.w - 210, l.y + 13, 92, 27, "MODS", 0xFF6860C9);
            drawAction(l.x + l.w - 110, l.y + 13, 92, 27, "SETTINGS", 0xFF25242B);
        } else if (globalSettings) {
            drawAction(l.x + l.w - 210, l.y + 13, 92, 27, "MODS", 0xFF302A36);
            drawAction(l.x + l.w - 110, l.y + 13, 92, 27, "SETTINGS", 0xFF6860C9);
        } else {
            drawAction(l.x + l.w - 116, l.y + 13, 98, 27, "BACK", 0xFF302A36);
        }
    }

    private void drawGlobalSettings(Layout l) {
        int cx = l.x + l.w / 2;
        int top = l.y + 91;
        drawCenteredString(fontRendererObj, "KYVORA CLIENT SETTINGS", cx, top, 0xFFE7D7FF);
        drawCenteredString(fontRendererObj, "Ajusta los controles del cliente. Los mods empiezan desactivados.",
                cx, top + 23, 0xFFB7B0BE);
        drawAction(cx - 135, top + 62, 270, 32,
                waitingForMenuKey ? "PULSA UNA TECLA..." : "OPEN MOD MENU: " + keyName(HudConfig.menuKeyCode),
                0xFF493B5D);
        drawScaleControl(cx - 135, top + 105);
        drawCenteredString(fontRendererObj, "Right Shift abre el menú · escala de 50% a 150%.", cx, top + 145, 0xFF99939F);
        drawAction(cx - 116, l.y + l.h - 67, 232, 31, "EDIT HUD LAYOUT", 0xFF4E75B9);
        drawCenteredString(fontRendererObj, "ESC vuelve a la lista de mods", cx, l.y + l.h - 25, 0xFF89848F);
    }

    private void drawModBrowser(Layout l, int mouseX, int mouseY) {
        int contentLeft = l.contentLeft;
        int contentWidth = l.contentWidth;
        int top = l.y + 64;
        boolean searchBelow = contentWidth < 510;

        if (l.showSidebar) {
            Gui.drawRect(l.x + 1, l.y + HEADER_HEIGHT + 1, contentLeft - 12, l.y + l.h - 1, 0x77100F15);
            drawSidebarFilter(l, "Default", "ALL", top + 4);
            drawSidebarFilter(l, "PvP", "PVP", top + 34);
            drawSidebarFilter(l, "HUD", "HUD", top + 64);
            drawSidebarFilter(l, "Utilities", "UTILITY", top + 94);
            drawAction(l.x + 12, l.y + l.h - 40, contentLeft - l.x - 24, 25, "KYVORA PROFILE", 0xFF302A36);
        }

        int filterX = contentLeft;
        for (String filter : FILTERS) {
            int w = filter.equals("UTILITY") ? 82 : 62;
            drawAction(filterX, top, w, 23, filter, selectedFilter.equals(filter) ? 0xFF6860C9 : 0xFF34323A);
            filterX += w + 7;
        }

        int searchX = searchBelow ? contentLeft : contentLeft + contentWidth - 185;
        int searchY = top + (searchBelow ? 28 : 0);
        int searchWidth = searchBelow ? contentWidth : 185;
        Gui.drawRect(searchX, searchY, searchX + searchWidth, searchY + 24, searchFocused ? 0xFF383140 : 0xFF28272E);
        drawString(fontRendererObj, searchText.isEmpty() ? "Search mods..." : searchText,
                searchX + 8, searchY + 8, searchText.isEmpty() ? 0xFF85818D : 0xFFEDEAF2);

        int listTop = l.y + (searchBelow ? 128 : 98);
        int listBottom = l.y + l.h - 33;
        int listHeight = Math.max(CARD_HEIGHT, listBottom - listTop);
        int columns = contentWidth >= 650 ? 3 : contentWidth >= 430 ? 2 : 1;
        int cardWidth = (contentWidth - CARD_GAP * (columns - 1)) / columns;
        int visibleRows = Math.max(1, (listHeight + CARD_GAP) / (CARD_HEIGHT + CARD_GAP));
        List<Integer> matches = getMatches();
        int rowCount = (matches.size() + columns - 1) / columns;
        scrollRow = Math.max(0, Math.min(scrollRow, Math.max(0, rowCount - visibleRows)));
        int firstIndex = scrollRow * columns;
        int lastIndex = Math.min(matches.size(), firstIndex + visibleRows * columns);

        for (int index = firstIndex; index < lastIndex; index++) {
            int row = index / columns - scrollRow;
            int column = index % columns;
            int x = contentLeft + column * (cardWidth + CARD_GAP);
            int y = listTop + row * (CARD_HEIGHT + CARD_GAP);
            drawModCard(MODS[matches.get(index)], x, y, cardWidth, mouseX, mouseY);
        }

        if (matches.isEmpty()) {
            drawCenteredString(fontRendererObj, "No hay mods con ese nombre o filtro.",
                    contentLeft + contentWidth / 2, listTop + 28, 0xFFAAA5B1);
        }

        drawString(fontRendererObj, contentWidth < 450 ? "Right Shift abre/cierra" : "Click en ENABLED para activar · Right Shift abre/cierra",
                contentLeft, l.y + l.h - 22, 0xFF89848F);
        String page = matches.isEmpty() ? "0 MODS" : (scrollRow + 1) + " / " + Math.max(1, rowCount) + " PAGES";
        drawString(fontRendererObj, page, l.x + l.w - 112, l.y + l.h - 22, 0xFFB8B0C1);
    }

    private void drawSidebarFilter(Layout l, String label, String filter, int y) {
        boolean active = selectedFilter.equals(filter);
        if (active) Gui.drawRect(l.x + 8, y - 4, l.x + 8 + l.sidebarWidth - 16, y + 20, 0xFF39323F);
        drawString(fontRendererObj, active ? ">  " + label : "   " + label,
                l.x + 16, y + 3, active ? 0xFFE1C9FF : 0xFFB3ADB9);
    }

    private void drawModCard(ModEntry mod, int x, int y, int w, int mouseX, int mouseY) {
        boolean enabled = isEnabled(mod.id);
        boolean hovered = inside(mouseX, mouseY, x, y, w, CARD_HEIGHT);
        Gui.drawRect(x, y, x + w, y + CARD_HEIGHT, hovered ? 0xFF302B36 : 0xFF25232A);
        Gui.drawRect(x, y, x + w, y + 1, enabled ? 0xFF8B66C6 : 0xFF49444F);
        Gui.drawRect(x, y + CARD_HEIGHT - 1, x + w, y + CARD_HEIGHT, 0xFF49444F);
        Gui.drawRect(x, y, x + 1, y + CARD_HEIGHT, 0xFF49444F);
        Gui.drawRect(x + w - 1, y, x + w, y + CARD_HEIGHT, 0xFF49444F);
        Gui.drawRect(x + w - 36, y + 9, x + w - 11, y + 21, enabled ? 0xFF2E8B61 : 0xFF4A4650);
        drawCenteredString(fontRendererObj, enabled ? "ON" : "OFF", x + w - 23, y + 12, 0xFFFFFFFF);
        Gui.drawRect(x + w / 2 - 16, y + 23, x + w / 2 + 16, y + 55, 0xFF4A3B5B);
        drawCenteredString(fontRendererObj, mod.icon, x + w / 2, y + 34, 0xFFE9D8FF);
        drawCenteredString(fontRendererObj, mod.title, x + w / 2, y + 63, 0xFFE4E0E8);
        drawCenteredString(fontRendererObj, fontRendererObj.trimStringToWidth(mod.description, w - 14),
                x + w / 2, y + 79, 0xFFAAA5B0);

        int buttonY = y + CARD_HEIGHT - 27;
        int leftWidth = (w - 3) / 2;
        drawAction(x + 1, buttonY, leftWidth, 26, "OPTIONS", 0xFF3B363F);
        drawAction(x + 2 + leftWidth, buttonY, w - leftWidth - 3, 26,
                enabled ? "ENABLED" : "DISABLED", enabled ? 0xFF258E5B : 0xFF49323B);
    }

    private void drawOptions(Layout l) {
        ModEntry mod = MODS[selectedMod];
        int cx = l.x + l.w / 2;
        int top = l.y + 82;
        drawCenteredString(fontRendererObj, mod.title + " OPTIONS", cx, top, 0xFFE7D7FF);
        drawCenteredString(fontRendererObj, mod.description, cx, top + 20, 0xFFB7B0BE);

        if (mod.id.equals("keystrokes")) {
            drawCenteredString(fontRendererObj, "El panel muestra teclas de movimiento, saltar y clics.",
                    cx, top + 57, 0xFFE2DEE8);
            drawAction(cx - 116, top + 88, 232, 30,
                    HudConfig.showShiftKey ? "SHIFT KEY: ON" : "SHIFT KEY: OFF", 0xFF37333D);
            drawCenteredString(fontRendererObj, "Puedes mover el panel desde Edit HUD Layout.",
                    cx, top + 135, 0xFF99939F);
        } else {
            drawCenteredString(fontRendererObj, "Este mod se activa o desactiva desde su tarjeta.",
                    cx, top + 57, 0xFFE2DEE8);
            drawCenteredString(fontRendererObj, "Usa Edit HUD Layout para arrastrar los paneles en pantalla.",
                    cx, top + 78, 0xFF99939F);
        }

        drawAction(cx - 116, l.y + l.h - 67, 232, 31, "EDIT HUD LAYOUT", 0xFF4E75B9);
        drawCenteredString(fontRendererObj, "ESC vuelve a la lista de mods", cx, l.y + l.h - 25, 0xFF89848F);
    }

    private void drawScaleControl(int x, int y) {
        Gui.drawRect(x, y, x + 270, y + 32, 0xFF37333D);
        drawString(fontRendererObj, "SIZE", x + 4, y + 12, 0xFFCBB7E4);
        drawAction(x + 27, y + 2, 26, 28, "-", 0xFF493B5D);
        drawAction(x + 238, y + 2, 28, 28, "+", 0xFF493B5D);
        int trackX = x + 59;
        int trackWidth = 116;
        int centerY = y + 16;
        Gui.drawRect(trackX, centerY - 2, trackX + trackWidth, centerY + 2, 0xFF8C8495);
        int knobX = trackX + (HudConfig.hudScalePercent - 50) * trackWidth / 100;
        Gui.drawRect(knobX - 3, centerY - 7, knobX + 4, centerY + 8, 0xFFC3A0FF);
        drawString(fontRendererObj, HudConfig.hudScalePercent + "%", x + 181, y + 12, 0xFFE6D8F6);
    }

    private List<Integer> getMatches() {
        List<Integer> result = new ArrayList<Integer>();
        String query = searchText.toLowerCase();
        for (int i = 0; i < MODS.length; i++) {
            ModEntry mod = MODS[i];
            if (!selectedFilter.equals("ALL") && !selectedFilter.equals(mod.category)) continue;
            if (!query.isEmpty() && !mod.title.toLowerCase().contains(query) &&
                    !mod.description.toLowerCase().contains(query)) continue;
            result.add(i);
        }
        return result;
    }

    private boolean isEnabled(String id) {
        switch (id) {
            case "keystrokes": return HudConfig.showKeystrokes;
            case "cps": return HudConfig.showCps;
            case "fps": return HudConfig.showFps;
            case "coordinates": return HudConfig.showCoordinates;
            case "ping": return HudConfig.showPing;
            case "kyvora": return HudConfig.showKyvoraTag;
            case "direction": return HudConfig.showDirection;
            case "biome": return HudConfig.showBiome;
            case "clock": return HudConfig.showClock;
            case "speed": return HudConfig.showSpeed;
            case "health": return HudConfig.showHealth;
            case "armor": return HudConfig.showArmor;
            case "sprint": return HudConfig.showSprint;
            case "target": return HudConfig.showTarget;
            default: return false;
        }
    }

    private void setEnabled(String id, boolean enabled) {
        switch (id) {
            case "keystrokes": HudConfig.showKeystrokes = enabled; break;
            case "cps": HudConfig.showCps = enabled; break;
            case "fps": HudConfig.showFps = enabled; break;
            case "coordinates": HudConfig.showCoordinates = enabled; break;
            case "ping": HudConfig.showPing = enabled; break;
            case "kyvora": HudConfig.showKyvoraTag = enabled; break;
            case "direction": HudConfig.showDirection = enabled; break;
            case "biome": HudConfig.showBiome = enabled; break;
            case "clock": HudConfig.showClock = enabled; break;
            case "speed": HudConfig.showSpeed = enabled; break;
            case "health": HudConfig.showHealth = enabled; break;
            case "armor": HudConfig.showArmor = enabled; break;
            case "sprint": HudConfig.showSprint = enabled; break;
            case "target": HudConfig.showTarget = enabled; break;
            default: return;
        }
        HudConfig.save();
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int mouseButton) throws IOException {
        if (mouseButton != 0) {
            super.mouseClicked(mouseX, mouseY, mouseButton);
            return;
        }

        Layout l = new Layout(width, height);
        if (inside(mouseX, mouseY, l.x + l.w - 44, l.y + 8, 38, 36)) {
            closeMenu();
            return;
        }

        if (inside(mouseX, mouseY, l.x + l.w - 216, l.y + 10, 98, 34)) {
            globalSettings = false;
            optionsView = false;
            return;
        }
        if (inside(mouseX, mouseY, l.x + l.w - 116, l.y + 10, 98, 34) && !optionsView) {
            globalSettings = true;
            searchFocused = false;
            return;
        }

        if (globalSettings) {
            handleGlobalSettingsClick(mouseX, mouseY, l);
            return;
        }

        if (optionsView) {
            handleOptionsClick(mouseX, mouseY, l);
            return;
        }

        if (inside(mouseX, mouseY, l.x + l.w - 118, l.y + 10, 108, 34)) {
            optionsView = true;
            return;
        }

        int top = l.y + 64;
        if (l.showSidebar && inside(mouseX, mouseY, l.x + 8, top, l.sidebarWidth - 8, 125)) {
            int slot = (mouseY - top) / 30;
            if (slot >= 0 && slot < 4) selectedFilter = new String[] {"ALL", "PVP", "HUD", "UTILITY"}[slot];
            scrollRow = 0;
            return;
        }

        int contentLeft = l.contentLeft;
        int contentWidth = l.contentWidth;
        int filterX = contentLeft;
        for (String filter : FILTERS) {
            int w = filter.equals("UTILITY") ? 82 : 62;
            if (inside(mouseX, mouseY, filterX, top, w, 23)) {
                selectedFilter = filter;
                scrollRow = 0;
                searchFocused = false;
                return;
            }
            filterX += w + 7;
        }

        boolean searchBelow = contentWidth < 510;
        int searchX = searchBelow ? contentLeft : contentLeft + contentWidth - 185;
        int searchY = top + (searchBelow ? 28 : 0);
        int searchWidth = searchBelow ? contentWidth : 185;
        searchFocused = inside(mouseX, mouseY, searchX, searchY, searchWidth, 24);
        if (searchFocused) return;

        int listTop = l.y + (searchBelow ? 128 : 98);
        if (mouseX >= contentLeft && mouseX < contentLeft + contentWidth &&
                mouseY >= listTop && mouseY <= l.y + l.h - 33) {
            int columns = contentWidth >= 650 ? 3 : contentWidth >= 430 ? 2 : 1;
            int cardWidth = (contentWidth - CARD_GAP * (columns - 1)) / columns;
            int row = (mouseY - listTop) / (CARD_HEIGHT + CARD_GAP) + scrollRow;
            int column = Math.min(columns - 1, Math.max(0, (mouseX - contentLeft) / (cardWidth + CARD_GAP)));
            int index = row * columns + column;
            List<Integer> matches = getMatches();
            if (index < matches.size()) {
                int cardX = contentLeft + column * (cardWidth + CARD_GAP);
                int localX = mouseX - cardX;
                int cardY = listTop + (row - scrollRow) * (CARD_HEIGHT + CARD_GAP);
                if (localX < 0 || localX >= cardWidth || mouseY < cardY || mouseY >= cardY + CARD_HEIGHT)
                    return;
                int buttonY = listTop + (row - scrollRow) * (CARD_HEIGHT + CARD_GAP) + CARD_HEIGHT - 27;
                if (mouseY >= buttonY && mouseY <= buttonY + 26) {
                    int modIndex = matches.get(index);
                    if (localX < cardWidth / 2) {
                        selectedMod = modIndex;
                        optionsView = true;
                    } else {
                        ModEntry mod = MODS[modIndex];
                        setEnabled(mod.id, !isEnabled(mod.id));
                    }
                }
            }
        }
    }

    private void handleGlobalSettingsClick(int mouseX, int mouseY, Layout l) {
        int cx = l.x + l.w / 2;
        int top = l.y + 91;
        if (inside(mouseX, mouseY, cx - 140, top + 58, 280, 40)) {
            waitingForMenuKey = true;
        } else if (inside(mouseX, mouseY, cx - 108, top + 105, 26, 32)) {
            setHudScale(HudConfig.hudScalePercent - 5);
        } else if (inside(mouseX, mouseY, cx - 76, top + 105, 116, 32)) {
            draggingScale = true;
            updateHudScaleFromMouse(mouseX, cx - 76);
        } else if (inside(mouseX, mouseY, cx + 103, top + 105, 32, 32)) {
            setHudScale(HudConfig.hudScalePercent + 5);
        } else if (inside(mouseX, mouseY, cx - 125, l.y + l.h - 72, 250, 42)) {
            HudConfig.editingPositions = true;
            mc.displayGuiScreen(null);
        }
    }

    private void setHudScale(int percent) {
        HudConfig.hudScalePercent = Math.max(50, Math.min(150, percent));
        HudConfig.save();
    }

    private void updateHudScaleFromMouse(int mouseX, int trackLeft) {
        int trackWidth = 116;
        int percent = 50 + Math.round((mouseX - trackLeft) * 100.0F / trackWidth);
        HudConfig.hudScalePercent = Math.max(50, Math.min(150, percent));
    }

    @Override
    protected void mouseClickMove(int mouseX, int mouseY, int clickedMouseButton, long timeSinceLastClick) {
        if (draggingScale && clickedMouseButton == 0 && globalSettings) {
            int cx = width / 2;
            updateHudScaleFromMouse(mouseX, cx - 76);
        }
        super.mouseClickMove(mouseX, mouseY, clickedMouseButton, timeSinceLastClick);
    }

    @Override
    protected void mouseReleased(int mouseX, int mouseY, int state) {
        if (draggingScale) HudConfig.save();
        draggingScale = false;
        super.mouseReleased(mouseX, mouseY, state);
    }

    private void handleOptionsClick(int mouseX, int mouseY, Layout l) {
        int cx = l.x + l.w / 2;
        int top = l.y + 82;
        ModEntry mod = MODS[selectedMod];
        if (inside(mouseX, mouseY, l.x + l.w - 124, l.y + 8, 116, 36)) {
            optionsView = false;
            return;
        }

        if (mod.id.equals("keystrokes") &&
                inside(mouseX, mouseY, cx - 125, top + 84, 250, 38)) {
            HudConfig.showShiftKey = !HudConfig.showShiftKey;
            HudConfig.save();
        }

        if (inside(mouseX, mouseY, cx - 125, l.y + l.h - 72, 250, 42)) {
            HudConfig.editingPositions = true;
            mc.displayGuiScreen(null);
        }
    }

    @Override
    protected void keyTyped(char typedChar, int keyCode) throws IOException {
        if (waitingForMenuKey) {
            if (keyCode == Keyboard.KEY_ESCAPE) {
                waitingForMenuKey = false;
            } else if (keyCode > 0 && keyCode < 256) {
                ClientProxy.setMenuKeyCode(keyCode);
                waitingForMenuKey = false;
            }
            return;
        }
        if (keyCode == Keyboard.KEY_ESCAPE) {
            if (optionsView || globalSettings) {
                optionsView = false;
                globalSettings = false;
            }
            else closeMenu();
            return;
        }
        if (searchFocused) {
            if (keyCode == Keyboard.KEY_BACK) {
                if (!searchText.isEmpty()) searchText = searchText.substring(0, searchText.length() - 1);
            } else if (typedChar >= 32 && typedChar < 127 && searchText.length() < 28) {
                searchText += typedChar;
            }
            scrollRow = 0;
            return;
        }
        super.keyTyped(typedChar, keyCode);
    }

    @Override
    public void handleMouseInput() throws IOException {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel == 0 || optionsView) return;
        Layout l = new Layout(width, height);
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        int listTop = l.y + (l.contentWidth < 510 ? 128 : 98);
        if (mouseX >= l.contentLeft && mouseY >= listTop && mouseY <= l.y + l.h - 33)
            scrollRow = Math.max(0, scrollRow + (wheel < 0 ? 1 : -1));
    }

    private void closeMenu() {
        HudConfig.editingPositions = false;
        mc.displayGuiScreen(null);
    }

    private String keyName(int keyCode) {
        String key = Keyboard.getKeyName(keyCode);
        return key == null ? "NONE" : key;
    }

    private void drawAction(int x, int y, int w, int h, String label, int color) {
        Gui.drawRect(x, y, x + w, y + h, color);
        Gui.drawRect(x, y, x + w, y + 1, 0xFF6E6675);
        drawCenteredString(fontRendererObj, label, x + w / 2, y + (h - fontRendererObj.FONT_HEIGHT) / 2,
                0xFFF1EDF5);
    }

    private static boolean inside(int mouseX, int mouseY, int x, int y, int w, int h) {
        return mouseX >= x && mouseX < x + w && mouseY >= y && mouseY < y + h;
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }

    private static final class ModEntry {
        final String id;
        final String title;
        final String description;
        final String category;
        final String icon;

        ModEntry(String id, String title, String description, String category, String icon) {
            this.id = id;
            this.title = title;
            this.description = description;
            this.category = category;
            this.icon = icon;
        }
    }

    private static final class Layout {
        final int x;
        final int y;
        final int w;
        final int h;
        final boolean showSidebar;
        final int sidebarWidth;
        final int contentLeft;
        final int contentWidth;

        Layout(int screenWidth, int screenHeight) {
            w = Math.max(300, Math.min(980, screenWidth - 20));
            h = Math.max(300, Math.min(640, screenHeight - 20));
            x = (screenWidth - w) / 2;
            y = (screenHeight - h) / 2;
            showSidebar = w >= 720;
            sidebarWidth = showSidebar ? 164 : 0;
            contentLeft = x + 16 + sidebarWidth;
            contentWidth = Math.max(220, x + w - 16 - contentLeft);
        }
    }
}
