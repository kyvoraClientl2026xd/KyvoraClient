package com.kyvora.hud;

import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.common.SidedProxy;
import net.minecraftforge.fml.common.event.FMLInitializationEvent;

@Mod(
        modid = KyvoraHudMod.MOD_ID,
        name = "Kyvora HUD",
        version = KyvoraHudMod.VERSION,
        acceptedMinecraftVersions = "[1.8.9]"
)
public final class KyvoraHudMod {
    public static final String MOD_ID = "kyvorahud";
    public static final String VERSION = "1.0.0";

    @SidedProxy(
            clientSide = "com.kyvora.hud.ClientProxy",
            serverSide = "com.kyvora.hud.CommonProxy"
    )
    public static CommonProxy proxy;

    @Mod.EventHandler
    public void initialize(FMLInitializationEvent event) {
        proxy.initialize();
    }
}
