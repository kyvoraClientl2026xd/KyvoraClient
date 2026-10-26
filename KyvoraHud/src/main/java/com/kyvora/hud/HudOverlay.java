package com.kyvora.hud;

import java.util.ArrayDeque;
import java.util.Deque;
import java.util.Iterator;
import java.util.Locale;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;
import net.minecraft.client.gui.ScaledResolution;
import net.minecraft.client.network.NetworkPlayerInfo;
import net.minecraft.client.settings.GameSettings;
import net.minecraft.entity.EntityLivingBase;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.util.BlockPos;
import net.minecraft.util.MovingObjectPosition;
import net.minecraftforge.client.event.MouseEvent;
import net.minecraftforge.client.event.RenderGameOverlayEvent;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
import net.minecraftforge.fml.common.gameevent.InputEvent;
import net.minecraftforge.fml.common.gameevent.TickEvent;
import net.minecraftforge.fml.common.FMLCommonHandler;
import org.lwjgl.opengl.GL11;
import org.lwjgl.input.Mouse;

public final class HudOverlay {
    private static final long CLICK_WINDOW_MS = 1000L;
    private static final int INFO_PANEL = 1;
    private static final int KEY_PANEL = 2;
    private static final int INFO_WIDTH = 178;
    private final Deque<Long> leftClicks = new ArrayDeque<Long>();
    private final Deque<Long> rightClicks = new ArrayDeque<Long>();
    private int draggingPanel;
    private int dragOffsetX;
    private int dragOffsetY;

    @SubscribeEvent
    public void onMouseInput(MouseEvent event) {
        Minecraft minecraft = Minecraft.getMinecraft();
        if (HudConfig.editingPositions) {
            if (event.button == 0 && event.buttonstate) {
                ScaledResolution resolution = new ScaledResolution(minecraft);
                int mouseX = getMouseX(resolution);
                int mouseY = getMouseY(resolution);
                draggingPanel = getPanelAt(mouseX, mouseY, resolution);
                if (draggingPanel != 0) {
                    dragOffsetX = mouseX - getPanelX(draggingPanel);
                    dragOffsetY = mouseY - getPanelY(draggingPanel);
                }
            }
            if (event.button >= 0) {
                event.setCanceled(true);
            }
            return;
        }

        if (minecraft.thePlayer == null || !event.buttonstate) {
            return;
        }

        long now = System.currentTimeMillis();
        if (event.button == 0) {
            leftClicks.addLast(now);
        } else if (event.button == 1) {
            rightClicks.addLast(now);
        }
        removeExpiredClicks(now);
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END || !HudConfig.editingPositions) {
            return;
        }

        if (!Mouse.isButtonDown(0)) {
            if (draggingPanel != 0) {
                draggingPanel = 0;
                HudConfig.save();
            }
            return;
        }

        if (draggingPanel == 0) {
            return;
        }

        Minecraft minecraft = Minecraft.getMinecraft();
        ScaledResolution resolution = new ScaledResolution(minecraft);
        int x = getMouseX(resolution) - dragOffsetX;
        int y = getMouseY(resolution) - dragOffsetY;
        setPanelPosition(draggingPanel, x, y, resolution);
    }

    @SubscribeEvent
    public void onRenderOverlay(RenderGameOverlayEvent.Post event) {
        if (event.type != RenderGameOverlayEvent.ElementType.TEXT) {
            return;
        }

        Minecraft minecraft = Minecraft.getMinecraft();
        EntityPlayer player = minecraft.thePlayer;
        if (player == null || minecraft.theWorld == null) {
            return;
        }

        FontRenderer font = minecraft.fontRendererObj;
        long now = System.currentTimeMillis();
        removeExpiredClicks(now);

        if (hasInfoWidgets()) {
            drawInfoPanel(minecraft, font, player);
        }
        if (HudConfig.showKeystrokes || HudConfig.showCps) {
            drawKeyPanel(minecraft, font);
        }
        if (HudConfig.editingPositions) {
            String message = "ARRASTRA LOS PANELES  ·  Shift derecho: terminar";
            font.drawStringWithShadow(message,
                    (event.resolution.getScaledWidth() - font.getStringWidth(message)) / 2,
                    12, 0xFFFFB3F0);
        }
    }

    private void drawInfoPanel(Minecraft minecraft, FontRenderer font, EntityPlayer player) {
        int x = HudConfig.infoX;
        int y = HudConfig.infoY;
        float scale = HudConfig.hudScalePercent / 100.0F;
        int height = 8 + getInfoLines(minecraft, player) * (font.FONT_HEIGHT + 4);
        GL11.glPushMatrix();
        GL11.glTranslatef(x * (1.0F - scale), y * (1.0F - scale), 0.0F);
        GL11.glScalef(scale, scale, 1.0F);
        try {
            drawPanelBackground(x, y, INFO_WIDTH, height);
            int lineY = y + 4;

            if (HudConfig.showFps) lineY = drawLine(font, "FPS  " + Minecraft.getDebugFPS(), x, lineY);
            if (HudConfig.showPing && minecraft.getNetHandler() != null) {
                NetworkPlayerInfo info = minecraft.getNetHandler().getPlayerInfo(player.getUniqueID());
                if (info != null) lineY = drawLine(font, "PING  " + info.getResponseTime() + " ms", x, lineY);
            }
            if (HudConfig.showCoordinates) {
                lineY = drawLine(font, String.format(Locale.ROOT, "XYZ  %d  %d  %d",
                        (int) Math.floor(player.posX), (int) Math.floor(player.posY),
                        (int) Math.floor(player.posZ)), x, lineY);
            }
            if (HudConfig.showDirection) lineY = drawLine(font, "DIRECCIÓN  " + getDirection(player.rotationYaw), x, lineY);
            if (HudConfig.showBiome) {
                BlockPos blockPos = new BlockPos(player.posX, player.posY, player.posZ);
                String biome = player.worldObj.getBiomeGenForCoords(blockPos).biomeName;
                lineY = drawLine(font, "BIOMA  " + font.trimStringToWidth(biome, INFO_WIDTH - 50), x, lineY);
            }
            if (HudConfig.showClock && minecraft.theWorld != null) {
                long clockTicks = (minecraft.theWorld.getWorldTime() + 6000L) % 24000L;
                lineY = drawLine(font, String.format(Locale.ROOT, "HORA  %02d:%02d",
                        clockTicks / 1000L, (clockTicks % 1000L) * 60L / 1000L), x, lineY);
            }
            if (HudConfig.showSpeed) {
                double speed = Math.sqrt(player.motionX * player.motionX + player.motionZ * player.motionZ) * 20.0D;
                lineY = drawLine(font, String.format(Locale.ROOT, "VELOCIDAD  %.1f m/s", speed), x, lineY);
            }
            if (HudConfig.showHealth) {
                lineY = drawLine(font, String.format(Locale.ROOT, "VIDA  %.0f/%.0f · HAMBRE %d",
                        player.getHealth(), player.getMaxHealth(), player.getFoodStats().getFoodLevel()), x, lineY);
            }
            if (HudConfig.showArmor) lineY = drawLine(font, "ARMADURA  " + player.getTotalArmorValue(), x, lineY);
            if (HudConfig.showSprint) lineY = drawLine(font, "SPRINT  " + (player.isSprinting() ? "ON" : "OFF"), x, lineY);
            if (HudConfig.showTarget && minecraft.objectMouseOver != null &&
                    minecraft.objectMouseOver.typeOfHit == MovingObjectPosition.MovingObjectType.ENTITY &&
                    minecraft.objectMouseOver.entityHit instanceof EntityLivingBase) {
                EntityLivingBase target = (EntityLivingBase) minecraft.objectMouseOver.entityHit;
                String text = String.format(Locale.ROOT, "OBJETIVO  %s · %.0f HP", target.getName(), target.getHealth());
                drawLine(font, font.trimStringToWidth(text, INFO_WIDTH - 14), x, lineY);
            }

            if (HudConfig.editingPositions) drawEditBorder(x, y, INFO_WIDTH, height);
        } finally {
            GL11.glPopMatrix();
        }
    }

    private int drawLine(FontRenderer font, String text, int x, int y) {
        font.drawStringWithShadow(text, x + 7, y, 0xFFE8EAF0);
        return y + font.FONT_HEIGHT + 4;
    }

    private static String getDirection(float yaw) {
        String[] directions = {"S", "SW", "W", "NW", "N", "NE", "E", "SE"};
        int index = Math.round((((yaw % 360.0F) + 360.0F) % 360.0F) / 45.0F) % directions.length;
        return directions[index];
    }

    private static boolean hasInfoWidgets() {
        return HudConfig.showFps || HudConfig.showPing || HudConfig.showCoordinates || HudConfig.showDirection ||
                HudConfig.showBiome || HudConfig.showClock || HudConfig.showSpeed || HudConfig.showHealth ||
                HudConfig.showArmor || HudConfig.showSprint || HudConfig.showTarget;
    }

    private static int getInfoLines(Minecraft minecraft, EntityPlayer player) {
        int lines = (HudConfig.showFps ? 1 : 0) + (HudConfig.showCoordinates ? 1 : 0) +
                (HudConfig.showDirection ? 1 : 0) + (HudConfig.showBiome ? 1 : 0) +
                (HudConfig.showClock && minecraft.theWorld != null ? 1 : 0) + (HudConfig.showSpeed ? 1 : 0) +
                (HudConfig.showHealth ? 1 : 0) + (HudConfig.showArmor ? 1 : 0) + (HudConfig.showSprint ? 1 : 0);
        if (HudConfig.showPing && minecraft.getNetHandler() != null &&
                minecraft.getNetHandler().getPlayerInfo(player.getUniqueID()) != null) lines++;
        if (HudConfig.showTarget && minecraft.objectMouseOver != null &&
                minecraft.objectMouseOver.typeOfHit == MovingObjectPosition.MovingObjectType.ENTITY &&
                minecraft.objectMouseOver.entityHit instanceof EntityLivingBase) lines++;
        return lines;
    }

    private void drawKeyPanel(Minecraft minecraft, FontRenderer font) {
        GameSettings keys = minecraft.gameSettings;
        int x = HudConfig.keystrokesX;
        int y = HudConfig.keystrokesY;
        int height = (HudConfig.showKeystrokes ? getKeystrokesHeight() : 0) + (HudConfig.showCps ? 19 : 0);
        float scale = HudConfig.hudScalePercent / 100.0F;
        GL11.glPushMatrix();
        GL11.glTranslatef(x * (1.0F - scale), y * (1.0F - scale), 0.0F);
        GL11.glScalef(scale, scale, 1.0F);
        try {
            Gui.drawRect(x, y, x + 72, y + height, 0xA60D111A);
            if (HudConfig.showKeystrokes) {
            drawKey(font, x + 25, y + 4, 22, 17, "W", keys.keyBindForward.isKeyDown());
            drawKey(font, x + 2, y + 23, 22, 17, "A", keys.keyBindLeft.isKeyDown());
            drawKey(font, x + 25, y + 23, 22, 17, "S", keys.keyBindBack.isKeyDown());
            drawKey(font, x + 48, y + 23, 22, 17, "D", keys.keyBindRight.isKeyDown());
            drawKey(font, x + 2, y + 42, 33, 17, "LMB", Mouse.isButtonDown(0));
            drawKey(font, x + 37, y + 42, 33, 17, "RMB", Mouse.isButtonDown(1));
            drawKey(font, x + 2, y + 61, 68, 14, "SPACE", keys.keyBindJump.isKeyDown());
            if (HudConfig.showShiftKey) {
                drawKey(font, x + 2, y + 78, 68, 17, "SHIFT", keys.keyBindSneak.isKeyDown());
            }
            }
            if (HudConfig.showCps) {
                font.drawStringWithShadow("CPS  " + leftClicks.size() + " / " + rightClicks.size(),
                        x + 4, y + (HudConfig.showKeystrokes ? getKeystrokesHeight() : 5), 0xFFFFFFFF);
            }
            if (HudConfig.editingPositions) {
                drawEditBorder(x, y, 72, height);
            }
        } finally {
            GL11.glPopMatrix();
        }
    }

    private int getPanelAt(int mouseX, int mouseY, ScaledResolution resolution) {
        if (HudConfig.showKeystrokes || HudConfig.showCps) {
            int keyHeight = (HudConfig.showKeystrokes ? getKeystrokesHeight() : 0) + (HudConfig.showCps ? 19 : 0);
            float scale = HudConfig.hudScalePercent / 100.0F;
            if (contains(mouseX, mouseY, HudConfig.keystrokesX, HudConfig.keystrokesY,
                    Math.round(72 * scale), Math.round(keyHeight * scale))) {
                return KEY_PANEL;
            }
        }
        if (hasInfoWidgets()) {
            int lines = getInfoLines(Minecraft.getMinecraft(), Minecraft.getMinecraft().thePlayer);
            int height = 8 + lines * (Minecraft.getMinecraft().fontRendererObj.FONT_HEIGHT + 4);
            float scale = HudConfig.hudScalePercent / 100.0F;
            if (contains(mouseX, mouseY, HudConfig.infoX, HudConfig.infoY,
                    Math.round(INFO_WIDTH * scale), Math.round(height * scale))) {
                return INFO_PANEL;
            }
        }
        return 0;
    }

    private static void setPanelPosition(int panel, int x, int y, ScaledResolution resolution) {
        int width = panel == INFO_PANEL ? INFO_WIDTH : 72;
        int height;
        if (panel == INFO_PANEL) {
            int lines = getInfoLines(Minecraft.getMinecraft(), Minecraft.getMinecraft().thePlayer);
            height = 8 + lines * (Minecraft.getMinecraft().fontRendererObj.FONT_HEIGHT + 4);
            float scale = HudConfig.hudScalePercent / 100.0F;
            HudConfig.infoX = clamp(x, 0, Math.max(0, resolution.getScaledWidth() - Math.round(width * scale)));
            HudConfig.infoY = clamp(y, 0, Math.max(0, resolution.getScaledHeight() - Math.round(height * scale)));
        } else {
            height = (HudConfig.showKeystrokes ? getKeystrokesHeight() : 0) + (HudConfig.showCps ? 19 : 0);
            float scale = HudConfig.hudScalePercent / 100.0F;
            HudConfig.keystrokesX = clamp(x, 0, Math.max(0, resolution.getScaledWidth() - Math.round(width * scale)));
            HudConfig.keystrokesY = clamp(y, 0, Math.max(0, resolution.getScaledHeight() - Math.round(height * scale)));
        }
    }

    private static int getPanelX(int panel) {
        return panel == INFO_PANEL ? HudConfig.infoX : HudConfig.keystrokesX;
    }

    private static int getPanelY(int panel) {
        return panel == INFO_PANEL ? HudConfig.infoY : HudConfig.keystrokesY;
    }

    private static int getMouseX(ScaledResolution resolution) {
        return Mouse.getX() * resolution.getScaledWidth() / Minecraft.getMinecraft().displayWidth;
    }

    private static int getMouseY(ScaledResolution resolution) {
        return resolution.getScaledHeight() -
                Mouse.getY() * resolution.getScaledHeight() / Minecraft.getMinecraft().displayHeight - 1;
    }

    private static boolean contains(int x, int y, int left, int top, int width, int height) {
        return x >= left && x <= left + width && y >= top && y <= top + height;
    }

    private static int clamp(int value, int min, int max) {
        return Math.max(min, Math.min(max, value));
    }

    private static int getKeystrokesHeight() {
        return HudConfig.showShiftKey ? 99 : 80;
    }

    private static void drawPanelBackground(int x, int y, int width, int height) {
        Gui.drawRect(x, y, x + width, y + height, 0xA60D111A);
    }

    private static void drawEditBorder(int x, int y, int width, int height) {
        int color = 0xFFFF83D5;
        Gui.drawRect(x, y, x + width, y + 1, color);
        Gui.drawRect(x, y + height - 1, x + width, y + height, color);
        Gui.drawRect(x, y, x + 1, y + height, color);
        Gui.drawRect(x + width - 1, y, x + width, y + height, color);
    }

    private static void drawKey(FontRenderer font, int x, int y, int width, int height,
                                String label, boolean pressed) {
        int background = pressed ? 0xFF7066C8 : 0xCC252A35;
        int foreground = pressed ? 0xFFFFFFFF : 0xFFD0D5DF;
        Gui.drawRect(x, y, x + width, y + height, background);
        font.drawStringWithShadow(label, x + (width - font.getStringWidth(label)) / 2,
                y + (height - font.FONT_HEIGHT) / 2, foreground);
    }

    private void removeExpiredClicks(long now) {
        removeExpiredClicks(leftClicks, now);
        removeExpiredClicks(rightClicks, now);
    }

    private static void removeExpiredClicks(Deque<Long> clicks, long now) {
        Iterator<Long> iterator = clicks.iterator();
        while (iterator.hasNext()) {
            if (now - iterator.next() > CLICK_WINDOW_MS) {
                iterator.remove();
            }
        }
    }
}
