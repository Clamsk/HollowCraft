import java.io.*;
import java.nio.file.*;
import java.util.zip.*;
import org.objectweb.asm.*;

/** Local build dependency only: repair package access after Yarn package remapping. */
public final class NativeAccessFixer {
    private static int access(int value){return (value&Opcodes.ACC_PRIVATE)==0?(value&~Opcodes.ACC_PROTECTED)|Opcodes.ACC_PUBLIC:value;}
    public static void main(String[] args) throws Exception {
        try(var source=new ZipFile(args[0]);var output=new ZipOutputStream(Files.newOutputStream(Path.of(args[1])))) {
            var entries=source.entries();while(entries.hasMoreElements()){var entry=entries.nextElement();if(entry.isDirectory())continue;byte[] bytes;try(var stream=source.getInputStream(entry)){bytes=stream.readAllBytes();}
                if(entry.getName().endsWith(".class")){var writer=new ClassWriter(0);new ClassReader(bytes).accept(new ClassVisitor(Opcodes.ASM9,writer){
                    @Override public void visit(int version,int flags,String name,String signature,String parent,String[] interfaces){super.visit(version,access(flags),name,signature,parent,interfaces);}
                    @Override public void visitInnerClass(String name,String parent,String inner,int flags){super.visitInnerClass(name,parent,inner,access(flags));}
                    @Override public FieldVisitor visitField(int flags,String name,String descriptor,String signature,Object value){return super.visitField(access(flags),name,descriptor,signature,value);}
                    @Override public MethodVisitor visitMethod(int flags,String name,String descriptor,String signature,String[] exceptions){return super.visitMethod(name.equals("<clinit>")?flags:access(flags),name,descriptor,signature,exceptions);}
                },0);bytes=writer.toByteArray();}
                output.putNextEntry(new ZipEntry(entry.getName()));output.write(bytes);output.closeEntry();
            }
        }
    }
}
