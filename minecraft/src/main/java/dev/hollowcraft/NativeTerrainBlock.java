package dev.hollowcraft;

import java.util.*;
import net.minecraft.block.*;
import net.minecraft.registry.*;
import net.minecraft.util.Identifier;
import net.minecraft.util.math.BlockPos;
import net.minecraft.util.shape.*;
import net.minecraft.world.BlockView;

/** Transient native collision, with the actual platform height instead of a full barrier cube. */
final class NativeTerrainBlock extends Block {
    static final NativeTerrainBlock INSTANCE = new NativeTerrainBlock();
    static final Map<BlockPos,VoxelShape> shapes = new HashMap<>();
    private NativeTerrainBlock() {super(AbstractBlock.Settings.create().strength(-1,3600000).nonOpaque().dropsNothing().dynamicBounds());}
    static void register() {Registry.register(Registries.BLOCK,new Identifier("hollowcraft_bridge","native_terrain"),INSTANCE);}
    static boolean proxy(BlockState state) {return state.isOf(INSTANCE)||state.isOf(Blocks.BARRIER);}
    @Override public VoxelShape getCollisionShape(BlockState state,BlockView world,BlockPos pos,ShapeContext context) {return shapes.getOrDefault(pos,VoxelShapes.fullCube());}
    @Override public VoxelShape getOutlineShape(BlockState state,BlockView world,BlockPos pos,ShapeContext context) {return getCollisionShape(state,world,pos,context);}
}
