package com.kyvora.hud;

import net.minecraft.client.Minecraft;
import net.minecraftforge.common.MinecraftForge;
import net.minecraftforge.fml.common.FMLCommonHandler;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
import net.minecraftforge.fml.common.gameevent.TickEvent;
import org.lwjgl.input.Keyboard;

public final class ClientProxy extends CommonProxy {
    private boolean menuKeyWasDown;

    @Override
    public void initialize() {
        HudConfig.load();
        MinecraftForge.EVENT_BUS.register(this);
        FMLCommonHandler.instance().bus().register(this);
        HudOverlay overlay = new HudOverlay();
        MinecraftForge.EVENT_BUS.register(overlay);
        FMLCommonHandler.instance().bus().register(overlay);
        MinecraftForge.EVENT_BUS.register(new HudNameTag());
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        Minecraft minecraft = Minecraft.getMinecraft();
        boolean keyDown = HudConfig.menuKeyCode > 0 && Keyboard.isKeyDown(HudConfig.menuKeyCode);
        boolean openMenu = keyDown && !menuKeyWasDown;
        menuKeyWasDown = keyDown;
        if (!openMenu || minecraft.thePlayer == null || minecraft.currentScreen != null) return;
        HudConfig.editingPositions = false;
        minecraft.displayGuiScreen(new HudSettingsScreen());
    }

    public static void setMenuKeyCode(int keyCode) {
        if (keyCode <= 0 || keyCode >= 256) return;
        HudConfig.menuKeyCode = keyCode;
        HudConfig.save();
    }
}
