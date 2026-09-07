# ==============================================================================
# BLENDER PYTHON SCRIPT: TV Console & Entertainment Unit Generator
# Generated from reference image
# Compatible with Blender 3.0+ / 4.0+
# ==============================================================================

import bpy
import os

def clear_scene():
    """Clear existing objects in scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(confirm=False)

def create_material(name, color, roughness=0.5, metallic=0.1):
    mat = bpy.data.materials.get(name)
    if not mat:
        mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        bsdf = nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs['Base Color'].default_value = color
            if 'Roughness' in bsdf.inputs:
                bsdf.inputs['Roughness'].default_value = roughness
            if 'Metallic' in bsdf.inputs:
                bsdf.inputs['Metallic'].default_value = metallic
    return mat

def build_tv_console():
    # Colors matching reference image
    wood_main_color = (0.42, 0.23, 0.10, 1.0)
    wood_trim_color = (0.32, 0.16, 0.06, 1.0)
    tv_body_color   = (0.05, 0.05, 0.06, 1.0)
    tv_screen_color = (0.08, 0.09, 0.10, 1.0)
    
    mat_wood_main = create_material("Mat_Wood_Main", wood_main_color, roughness=0.6, metallic=0.0)
    mat_wood_trim = create_material("Mat_Wood_Trim", wood_trim_color, roughness=0.5, metallic=0.0)
    mat_tv_body   = create_material("Mat_TV_Body", tv_body_color, roughness=0.3, metallic=0.7)
    mat_tv_screen = create_material("Mat_TV_Screen", tv_screen_color, roughness=0.1, metallic=0.2)
    
    # --------------------------------------------------------------------------
    # 1. BASE CABINET UNIT
    # --------------------------------------------------------------------------
    # Main lower cabinet box
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.35))
    cabinet = bpy.context.active_object
    cabinet.name = "TVConsole_BaseCabinet"
    cabinet.scale = (1.4, 0.45, 0.5)
    bpy.ops.object.transform_apply(scale=True)
    cabinet.data.materials.append(mat_wood_main)
    
    # Top Ledge Trim of Base Cabinet
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.625))
    cabinet_top = bpy.context.active_object
    cabinet_top.name = "TVConsole_CabinetTopTrim"
    cabinet_top.scale = (1.46, 0.49, 0.05)
    bpy.ops.object.transform_apply(scale=True)
    cabinet_top.data.materials.append(mat_wood_trim)
    
    # Bottom Base Ledge Trim
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.05))
    cabinet_bottom = bpy.context.active_object
    cabinet_bottom.name = "TVConsole_CabinetBottomTrim"
    cabinet_bottom.scale = (1.46, 0.49, 0.08)
    bpy.ops.object.transform_apply(scale=True)
    cabinet_bottom.data.materials.append(mat_wood_trim)

    # Cabinet Front Doors (Left & Right)
    for x_offset, side_name in [(-0.33, "Left"), (0.33, "Right")]:
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(x_offset, -0.23, 0.35))
        door = bpy.context.active_object
        door.name = f"TVConsole_Door_{side_name}"
        door.scale = (0.6, 0.03, 0.42)
        bpy.ops.object.transform_apply(scale=True)
        door.data.materials.append(mat_wood_main)
        
        # Door Knob
        knob_x = x_offset + (0.24 if side_name == "Left" else -0.24)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.025, location=(knob_x, -0.255, 0.35))
        knob = bpy.context.active_object
        knob.name = f"TVConsole_Knob_{side_name}"
        knob.data.materials.append(mat_wood_trim)

    # --------------------------------------------------------------------------
    # 2. TALL BACKBOARD & TOP SHELF
    # --------------------------------------------------------------------------
    # Tall Back Board attached to cabinet
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.12, 1.15))
    backboard = bpy.context.active_object
    backboard.name = "TVConsole_Backboard"
    backboard.scale = (1.15, 0.18, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    backboard.data.materials.append(mat_wood_main)

    # Top Roof / Overhang Cap on backboard
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.08, 1.68))
    roof = bpy.context.active_object
    roof.name = "TVConsole_TopShelf"
    roof.scale = (1.25, 0.28, 0.08)
    bpy.ops.object.transform_apply(scale=True)
    roof.data.materials.append(mat_wood_trim)
    
    # Roof Lip Ledge
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.05, 1.71))
    roof_lip = bpy.context.active_object
    roof_lip.name = "TVConsole_TopShelfLip"
    roof_lip.scale = (1.27, 0.04, 0.05)
    bpy.ops.object.transform_apply(scale=True)
    roof_lip.data.materials.append(mat_wood_trim)

    # --------------------------------------------------------------------------
    # 3. FLAT SCREEN TELEVISION
    # --------------------------------------------------------------------------
    # TV Base Plate
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.0, 0.66))
    tv_base = bpy.context.active_object
    tv_base.name = "TV_Base"
    tv_base.scale = (0.42, 0.22, 0.02)
    bpy.ops.object.transform_apply(scale=True)
    tv_base.data.materials.append(mat_tv_body)

    # TV Neck Stem
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.03, 0.72))
    tv_neck = bpy.context.active_object
    tv_neck.name = "TV_Neck"
    tv_neck.scale = (0.1, 0.08, 0.1)
    bpy.ops.object.transform_apply(scale=True)
    tv_neck.data.materials.append(mat_tv_body)

    # TV Outer Frame (Bezel)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.02, 1.1))
    tv_frame = bpy.context.active_object
    tv_frame.name = "TV_Frame"
    tv_frame.scale = (0.9, 0.04, 0.55)
    bpy.ops.object.transform_apply(scale=True)
    tv_frame.data.materials.append(mat_tv_body)

    # TV Display Glass Screen (Front Face)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.005, 1.1))
    tv_display = bpy.context.active_object
    tv_display.name = "TV_DisplayScreen"
    tv_display.scale = (0.85, 0.01, 0.50)
    bpy.ops.object.transform_apply(scale=True)
    tv_display.data.materials.append(mat_tv_screen)

    # --------------------------------------------------------------------------
    # 4. JOIN & EXPORT
    # --------------------------------------------------------------------------
    bpy.ops.object.select_all(action='SELECT')
    main_obj = cabinet
    bpy.context.view_layer.objects.active = main_obj
    main_obj.name = "TV_Entertainment_Console"
    bpy.ops.object.join()
    
    print("[Blender Generator] Successfully created 'TV_Entertainment_Console'!")

if __name__ == "__main__":
    clear_scene()
    build_tv_console()
    
    # Auto-export to Unity Assets/Models directory
    export_dir = "/Users/mihir/Desktop/Counter_Boom/Assets/Models"
    os.makedirs(export_dir, exist_ok=True)
    
    fbx_path = os.path.join(export_dir, "TV_Console.fbx")
    obj_path = os.path.join(export_dir, "TV_Console.obj")
    
    try:
        bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=False)
        print(f"[Blender Exporter] Exported FBX to: {fbx_path}")
    except Exception as e:
        print(f"[Blender Exporter] FBX export warning: {e}")
        
    try:
        bpy.ops.wm.obj_export(filepath=obj_path) if hasattr(bpy.ops.wm, 'obj_export') else bpy.ops.export_scene.obj(filepath=obj_path)
        print(f"[Blender Exporter] Exported OBJ to: {obj_path}")
    except Exception as e:
        print(f"[Blender Exporter] OBJ export warning: {e}")
