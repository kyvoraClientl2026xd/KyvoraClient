package com.kyvora.hud;

import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.entity.RenderManager;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.entity.EntityLivingBase;
import net.minecraftforge.client.event.RenderLivingEvent;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
import org.lwjgl.opengl.GL11;

public final class HudNameTag {
    @SubscribeEvent
    public void onRenderName(RenderLivingEvent.Specials.Post<EntityLivingBase> event) {
        Minecraft minecraft = Minecraft.getMinecraft();
        if (!HudConfig.showKyvoraTag || minecraft.thePlayer == null ||
                event.entity != minecraft.thePlayer || minecraft.gameSettings.thirdPersonView == 0) {
            return;
        }

        FontRenderer font = minecraft.fontRendererObj;
        RenderManager renderManager = minecraft.getRenderManager();
        String playerName = minecraft.thePlayer.getDisplayName().getFormattedText();
        int nameWidth = font.getStringWidth(playerName);
        int badgeX = -(nameWidth / 2) - 21;
        double labelY = event.y + event.entity.height + 0.5D;

        GL11.glPushMatrix();
        GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);
        try {
            GL11.glTranslatef((float) event.x, (float) labelY, (float) event.z);
            GL11.glNormal3f(0.0F, 1.0F, 0.0F);
            GL11.glRotatef(-renderManager.playerViewY, 0.0F, 1.0F, 0.0F);
            GL11.glRotatef(renderManager.playerViewX, 1.0F, 0.0F, 0.0F);
            float hudScale = HudConfig.hudScalePercent / 100.0F;
            GL11.glScalef(-0.025F * hudScale, -0.025F * hudScale, 0.025F * hudScale);
            GL11.glDisable(GL11.GL_LIGHTING);
            GL11.glDisable(GL11.GL_DEPTH_TEST);
            GL11.glDepthMask(false);
            GL11.glEnable(GL11.GL_BLEND);
            GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);

            GL11.glDisable(GL11.GL_TEXTURE_2D);
            drawCircle(badgeX + 8.0F, 5.0F, 8.0F, 0.53F, 0.34F, 0.96F, 0.98F);
            drawCircle(badgeX + 8.0F, 5.0F, 6.2F, 0.20F, 0.11F, 0.42F, 1.0F);
            GL11.glEnable(GL11.GL_TEXTURE_2D);
            font.drawStringWithShadow("K", badgeX + 5, 0, 0xFFF2EAFF);
        } finally {
            GL11.glPopAttrib();
            GL11.glPopMatrix();
        }
    }

    private static void drawCircle(float centerX, float centerY, float radius,
                                   float red, float green, float blue, float alpha) {
        GL11.glColor4f(red, green, blue, alpha);
        GL11.glBegin(GL11.GL_TRIANGLE_FAN);
        GL11.glVertex3f(centerX, centerY, 0.0F);
        for (int i = 0; i <= 24; i++) {
            double angle = Math.PI * 2.0D * i / 24.0D;
            GL11.glVertex3f(centerX + (float) Math.cos(angle) * radius,
                    centerY + (float) Math.sin(angle) * radius, 0.0F);
        }
        GL11.glEnd();
    }
}
