package dev.hollowcraft;

import com.google.gson.*;
import java.lang.reflect.Method;
import net.minecraft.item.ItemStack;
import net.minecraft.entity.attribute.EntityAttributes;
import net.minecraft.server.network.ServerPlayerEntity;

/** Reads the installed Better Combat registry; no copied weapon presets or hardcoded sword stats. */
final class CombatBridge {
    private static final Gson JSON = new Gson();
    private static Method attributes;
    static JsonObject profile(ServerPlayerEntity player) {
        JsonObject value = new JsonObject();
        value.addProperty("item", net.minecraft.registry.Registries.ITEM.getId(player.getMainHandStack().getItem()).toString());
        value.addProperty("damage", player.getAttributeValue(EntityAttributes.GENERIC_ATTACK_DAMAGE));
        value.addProperty("speed", player.getAttributeValue(EntityAttributes.GENERIC_ATTACK_SPEED));
        try {
            if (attributes == null) attributes = Class.forName("net.bettercombat.logic.WeaponRegistry").getMethod("getAttributes",ItemStack.class);
            Object weapon = attributes.invoke(null,player.getMainHandStack());
            if (weapon != null) {
                value.add("weapon", JSON.toJsonTree(weapon)); value.addProperty("source","Better Combat 1.8.5");
                return value;
            }
        } catch (ReflectiveOperationException e) { value.addProperty("error",e.toString()); }
        value.addProperty("source","vanilla unarmed/tool");
        JsonObject fallback = JsonParser.parseString("{\"attack_range\":2,\"attacks\":[{\"damage_multiplier\":1,\"angle\":90,\"upswing\":0.3,\"animation\":\"bettercombat:one_handed_punch\",\"hitbox\":\"HORIZONTAL_PLANE\"}]}").getAsJsonObject();
        value.add("weapon",fallback); return value;
    }
}
