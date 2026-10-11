package dev.hollowcraft.mixin;

import java.util.Deque;
import net.minecraft.client.util.math.MatrixStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;

@Mixin(MatrixStack.class)
public interface MatrixStackAccess {
    @Accessor("stack") Deque<MatrixStack.Entry> hollowcraft$stack();
}
