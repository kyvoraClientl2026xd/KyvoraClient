package com.example.examplemod;

import com.mojang.blaze3d.matrix.MatrixStack;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.screen.Screen;
import net.minecraft.client.settings.KeyBinding;
import net.minecraft.util.text.StringTextComponent;
import net.minecraftforge.client.event.RenderGameOverlayEvent;
import net.minecraftforge.common.MinecraftForge;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.client.registry.ClientRegistry;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.event.lifecycle.FMLClientSetupEvent;
import net.minecraftforge.fml.javafmlmod.FMLJavaModLoadingContext;
import org.lwjgl.glfw.GLFW;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.util.Properties;

@Mod("kyvorahud")
public final class KyvoraHud1165 {
    private static final int KEYSTROKES = 0;
    private static final int FPS = 1;
    private static final int COORDINATES = 2;
    private static final String[] MODS = {"Keystrokes", "FPS", "Coordinates"};
    private static final boolean[] ENABLED = new boolean[MODS.length];
    private static final KeyBinding MENU = new KeyBinding("key.kyvora.mods", 344, "key.categories.kyvora");
    private static boolean menuWasDown;
    private static boolean draggingScale;
    private static int scale = 100;
    private static File config;

    public KyvoraHud1165() {
        FMLJavaModLoadingContext.get().getModEventBus().addListener(this::clientSetup);
    }

    private void clientSetup(FMLClientSetupEvent event) {
        event.enqueueWork(() -> {
            ClientRegistry.registerKeyBinding(MENU);
            Minecraft mc = Minecraft.getInstance();
            config = new File(mc.gameDirectory, "config/kyvora-hud.properties");
            load();
            MinecraftForge.EVENT_BUS.register(this);
        });
    }

    @SubscribeEvent
    public void tick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        Minecraft mc = Minecraft.getInstance();
        boolean menuDown = MENU.isDown();
        if (menuDown && !menuWasDown && mc.player != null && mc.screen == null) {
            mc.setScreen(new ModsScreen());
        }
        menuWasDown = menuDown;
    }

    @SubscribeEvent
    public void render(RenderGameOverlayEvent.Text event) {
        Minecraft mc = Minecraft.getInstance();
        if (mc.player == null) return;
        MatrixStack m = event.getMatrixStack();
        m.pushPose();
        m.scale(scale / 100.0F, scale / 100.0F, 1.0F);
        int y = 6, step = 10;
        if (ENABLED[FPS]) { mc.font.draw(m, mc.fpsString, 6, y, 0xFFFFFF); y += step; }
        if (ENABLED[KEYSTROKES]) {
            boolean w = mc.options.keyUp.isDown(), a = mc.options.keyLeft.isDown(), s = mc.options.keyDown.isDown(), d = mc.options.keyRight.isDown();
            mc.font.draw(m, (w ? "§a" : "§f") + "[W]", 30, y, 0xFFFFFF);
            mc.font.draw(m, (a ? "§a" : "§f") + "[A] " + (s ? "§a" : "§f") + "[S] " + (d ? "§a" : "§f") + "[D]", 17, y + step, 0xFFFFFF);
            y += step * 2;
        }
        if (ENABLED[COORDINATES]) mc.font.draw(m, String.format("XYZ %.1f %.1f %.1f", mc.player.getX(), mc.player.getY(), mc.player.getZ()), 6, y, 0xFFFFFF);
        m.popPose();
    }

    private static void load() {
        Properties p = new Properties();
        try (FileInputStream in = new FileInputStream(config)) { p.load(in); } catch (Exception ignored) { }
        for (int i = 0; i < MODS.length; i++) ENABLED[i] = Boolean.parseBoolean(p.getProperty("mod." + i, "false"));
        try { scale = Math.max(50, Math.min(150, Integer.parseInt(p.getProperty("scale", "100")))); } catch (Exception ignored) { scale = 100; }
    }

    private static void save() {
        try {
            Properties p = new Properties();
            for (int i = 0; i < MODS.length; i++) p.setProperty("mod." + i, Boolean.toString(ENABLED[i]));
            p.setProperty("scale", Integer.toString(scale));
            File parent = config.getParentFile(); if (parent != null) parent.mkdirs();
            try (FileOutputStream out = new FileOutputStream(config)) { p.store(out, "Kyvora HUD settings"); }
        } catch (Exception ignored) { }
    }

    private static final class ModsScreen extends Screen {
        protected ModsScreen() { super(new StringTextComponent("KYVORA MODS")); }
        @Override public boolean isPauseScreen() { return false; }
        @Override public void render(MatrixStack m, int mouseX, int mouseY, float partialTicks) {
            renderBackground(m);
            int left = width / 2 - 150, top = height / 2 - 118;
            fill(m, left, top, left + 300, top + 236, 0xE6191922);
            drawCenteredString(m, font, "§5K §fKYVORA  ·  MODS", width / 2, top + 10, 0xFFFFFF);
            drawCenteredString(m, font, "Right Shift · toggle mods · scale " + scale + "%", width / 2, top + 24, 0xBBBBBB);
            for (int i = 0; i < MODS.length; i++) {
                int col = i % 2, row = i / 2;
                int x = left + 12 + col * 140, y = top + 43 + row * 25;
                boolean hover = mouseX >= x && mouseX < x + 132 && mouseY >= y && mouseY < y + 21;
                fill(m, x, y, x + 132, y + 21, hover ? 0xFF51406A : 0xFF302A38);
                drawString(m, font, (ENABLED[i] ? "§a● " : "§7○ ") + MODS[i], x + 7, y + 6, 0xFFFFFF);
            }
            int bottom = top + 207;
            fill(m, left + 12, bottom, left + 40, bottom + 20, 0xFF51406A);
            fill(m, left + 260, bottom, left + 288, bottom + 20, 0xFF51406A);
            drawCenteredString(m, font, "−", left + 26, bottom + 6, 0xFFFFFF);
            drawCenteredString(m, font, "+", left + 274, bottom + 6, 0xFFFFFF);
            int trackX = left + 58, trackWidth = 184, trackY = bottom + 10;
            fill(m, trackX, trackY - 2, trackX + trackWidth, trackY + 2, 0xFF8C8495);
            int knobX = trackX + (scale - 50) * trackWidth / 100;
            fill(m, knobX - 3, trackY - 7, knobX + 4, trackY + 8, 0xFFC3A0FF);
            super.render(m, mouseX, mouseY, partialTicks);
        }
        @Override public boolean mouseClicked(double mx, double my, int button) {
            int left = width / 2 - 150, top = height / 2 - 118;
            for (int i = 0; i < MODS.length; i++) {
                int x = left + 12 + (i % 2) * 140, y = top + 43 + (i / 2) * 25;
                if (mx >= x && mx < x + 132 && my >= y && my < y + 21) { ENABLED[i] = !ENABLED[i]; save(); return true; }
            }
            int bottom = top + 207;
            if (my >= bottom && my < bottom + 20) {
                if (mx >= left + 12 && mx < left + 40) scale = Math.max(50, scale - 5);
                else if (mx >= left + 260 && mx < left + 288) scale = Math.min(150, scale + 5);
                else if (mx >= left + 58 && mx <= left + 242) {
                    draggingScale = true;
                    updateScale(mx, left + 58);
                } else return super.mouseClicked(mx, my, button);
                save(); return true;
            }
            return super.mouseClicked(mx, my, button);
        }
        private void updateScale(double mouseX, int trackX) {
            scale = Math.max(50, Math.min(150, 50 + (int)Math.round((mouseX - trackX) * 100.0 / 184.0)));
            save();
        }
        @Override public boolean mouseDragged(double mouseX, double mouseY, int button, double dragX, double dragY) {
            if (draggingScale && button == 0) { updateScale(mouseX, width / 2 - 150 + 58); return true; }
            return super.mouseDragged(mouseX, mouseY, button, dragX, dragY);
        }
        @Override public boolean mouseReleased(double mouseX, double mouseY, int button) {
            draggingScale = false;
            return super.mouseReleased(mouseX, mouseY, button);
        }
        @Override public boolean keyPressed(int key, int scanCode, int modifiers) {
            if (key == GLFW.GLFW_KEY_RIGHT_SHIFT || key == GLFW.GLFW_KEY_ESCAPE) { Minecraft.getInstance().setScreen(null); return true; }
            return super.keyPressed(key, scanCode, modifiers);
        }
    }
}
