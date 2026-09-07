# ==============================================================================
# BLENDER PYTHON SCRIPT: Procedural Low-Poly Game Props Generator
# Compatible with Blender 3.0+ / 4.0+
#
# How to run in Blender:
# 1. Open Blender.
# 2. Go to the "Scripting" tab at the top.
# 3. Click "New" to create a new script, then paste this code into the editor.
# 4. Click the "Play" button (Run Script) or press Alt+P.
# 5. Check your 3D Viewport — procedural low-poly game props will be generated!
# ==============================================================================

import bpy
import math

def clear_scene():
    """Clear existing objects in the default Blender scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(confirm=False)

def create_material(name, color, roughness=0.4, metallic=0.1):
    """Utility to create a simple Principled BSDF material with color."""
    mat = bpy.data.materials.get(name)
    if not mat:
        mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        bsdf = nodes.get("Principled BSDF")
        if bsdf:
            # Set Base Color (RGBA)
            bsdf.inputs['Base Color'].default_value = color
            # Set Roughness
            if 'Roughness' in bsdf.inputs:
                bsdf.inputs['Roughness'].default_value = roughness
            # Set Metallic
            if 'Metallic' in bsdf.inputs:
                bsdf.inputs['Metallic'].default_value = metallic
    return mat

def build_explosive_barrel():
    """Generates a low-poly explosive barrel prop."""
    # Base cylinder
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16, radius=0.4, depth=1.2, location=(0, 0, 0.6)
    )
    barrel = bpy.context.active_object
    barrel.name = "Prop_Explosive_Barrel"
    
    # Create materials
    mat_red = create_material("Mat_Barrel_Red", (0.8, 0.1, 0.1, 1.0), roughness=0.3)
    mat_black = create_material("Mat_Barrel_Dark", (0.1, 0.1, 0.1, 1.0), roughness=0.6, metallic=0.8)
    mat_yellow = create_material("Mat_Hazard_Yellow", (0.9, 0.8, 0.0, 1.0), roughness=0.4)
    
    barrel.data.materials.append(mat_red)
    
    # Create metal rings (Top & Bottom)
    bpy.ops.mesh.primitive_torus_add(
        align='WORLD', location=(0, 0, 0.95),
        major_radius=0.41, minor_radius=0.035,
        major_segments=16, minor_segments=8
    )
    ring_top = bpy.context.active_object
    ring_top.name = "Barrel_Ring_Top"
    ring_top.data.materials.append(mat_black)
    
    bpy.ops.mesh.primitive_torus_add(
        align='WORLD', location=(0, 0, 0.25),
        major_radius=0.41, minor_radius=0.035,
        major_segments=16, minor_segments=8
    )
    ring_bottom = bpy.context.active_object
    ring_bottom.name = "Barrel_Ring_Bottom"
    ring_bottom.data.materials.append(mat_black)
    
    # Hazard Band around center
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16, radius=0.405, depth=0.25, location=(0, 0, 0.6)
    )
    hazard_band = bpy.context.active_object
    hazard_band.name = "Barrel_Hazard_Band"
    hazard_band.data.materials.append(mat_yellow)
    
    # Join parts into single mesh
    bpy.ops.object.select_all(action='DESELECT')
    barrel.select_set(True)
    ring_top.select_set(True)
    ring_bottom.select_set(True)
    hazard_band.select_set(True)
    bpy.context.view_layer.objects.active = barrel
    bpy.ops.object.join()
    
    print("[Blender Generator] Created 'Prop_Explosive_Barrel'")
    return barrel

def build_supply_crate():
    """Generates a low-poly military supply crate."""
    # Main Box
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(2.0, 0, 0.5))
    crate = bpy.context.active_object
    crate.name = "Prop_Supply_Crate"
    
    mat_wood = create_material("Mat_Crate_Wood", (0.35, 0.22, 0.12, 1.0), roughness=0.7)
    mat_metal_trim = create_material("Mat_Crate_Trim", (0.15, 0.18, 0.15, 1.0), roughness=0.4, metallic=0.7)
    
    crate.data.materials.append(mat_wood)
    
    # Metal reinforcement corners
    corner_offsets = [
        (-0.5, -0.5, 0.0), (0.5, -0.5, 0.0),
        (-0.5, 0.5, 0.0), (0.5, 0.5, 0.0),
        (-0.5, -0.5, 1.0), (0.5, -0.5, 1.0),
        (-0.5, 0.5, 1.0), (0.5, 0.5, 1.0),
    ]
    
    corners = []
    for idx, offset in enumerate(corner_offsets):
        x = 2.0 + offset[0]
        y = offset[1]
        z = offset[2]
        bpy.ops.mesh.primitive_cube_add(size=0.15, location=(x, y, z))
        corner = bpy.context.active_object
        corner.name = f"Crate_Corner_{idx}"
        corner.data.materials.append(mat_metal_trim)
        corners.append(corner)
        
    # Join corners to crate
    bpy.ops.object.select_all(action='DESELECT')
    crate.select_set(True)
    for c in corners:
        c.select_set(True)
    bpy.context.view_layer.objects.active = crate
    bpy.ops.object.join()
    
    print("[Blender Generator] Created 'Prop_Supply_Crate'")
    return crate

def build_ammo_box():
    """Generates a low-poly ammunition box."""
    # Main Box
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(-2.0, 0, 0.25))
    box = bpy.context.active_object
    box.scale = (0.6, 0.35, 0.25)
    box.name = "Prop_Ammo_Box"
    bpy.ops.object.transform_apply(scale=True)
    
    # Lid
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(-2.0, 0, 0.52))
    lid = bpy.context.active_object
    lid.scale = (0.62, 0.37, 0.05)
    lid.name = "Ammo_Box_Lid"
    bpy.ops.object.transform_apply(scale=True)
    
    # Handle
    bpy.ops.mesh.primitive_torus_add(
        align='WORLD', location=(-2.0, 0, 0.58),
        major_radius=0.1, minor_radius=0.015,
        major_segments=12, minor_segments=6
    )
    handle = bpy.context.active_object
    handle.name = "Ammo_Box_Handle"
    
    # Materials
    mat_green = create_material("Mat_Ammo_Green", (0.12, 0.28, 0.12, 1.0), roughness=0.4, metallic=0.3)
    mat_metal = create_material("Mat_Ammo_Latch", (0.2, 0.2, 0.2, 1.0), roughness=0.3, metallic=0.9)
    
    box.data.materials.append(mat_green)
    lid.data.materials.append(mat_green)
    handle.data.materials.append(mat_metal)
    
    # Join into single object
    bpy.ops.object.select_all(action='DESELECT')
    box.select_set(True)
    lid.select_set(True)
    handle.select_set(True)
    bpy.context.view_layer.objects.active = box
    bpy.ops.object.join()
    
    print("[Blender Generator] Created 'Prop_Ammo_Box'")
    return box

def build_kitchen_blender():
    """Generates a low-poly kitchen blender appliance model."""
    # Base Motor Unit
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16, radius=0.25, depth=0.4, location=(4.0, 0, 0.2)
    )
    base_unit = bpy.context.active_object
    base_unit.name = "Prop_Kitchen_Blender"
    
    # Glass Pitcher
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=12, radius=0.2, depth=0.6, location=(4.0, 0, 0.7)
    )
    pitcher = bpy.context.active_object
    pitcher.name = "Blender_Pitcher"
    
    # Lid
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=12, radius=0.21, depth=0.08, location=(4.0, 0, 1.02)
    )
    lid = bpy.context.active_object
    lid.name = "Blender_Lid"
    
    # Control Knob
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=12, radius=0.06, depth=0.06, location=(4.0, -0.25, 0.2)
    )
    knob = bpy.context.active_object
    knob.rotation_euler = (math.radians(90), 0, 0)
    knob.name = "Blender_Knob"
    
    # Materials
    mat_metal = create_material("Mat_Blender_Base", (0.8, 0.8, 0.85, 1.0), roughness=0.2, metallic=0.9)
    mat_glass = create_material("Mat_Blender_Glass", (0.6, 0.8, 0.9, 0.4), roughness=0.1, metallic=0.1)
    mat_rubber = create_material("Mat_Blender_Dark", (0.1, 0.1, 0.1, 1.0), roughness=0.8)
    
    base_unit.data.materials.append(mat_metal)
    pitcher.data.materials.append(mat_glass)
    lid.data.materials.append(mat_rubber)
    knob.data.materials.append(mat_rubber)
    
    # Join parts
    bpy.ops.object.select_all(action='DESELECT')
    base_unit.select_set(True)
    pitcher.select_set(True)
    lid.select_set(True)
    knob.select_set(True)
    bpy.context.view_layer.objects.active = base_unit
    bpy.ops.object.join()
    
    print("[Blender Generator] Created 'Prop_Kitchen_Blender'")
    return base_unit

# Run Generator
if __name__ == "__main__":
    clear_scene()
    build_explosive_barrel()
    build_supply_crate()
    build_ammo_box()
    build_kitchen_blender()
    
    # Select all and adjust view
    bpy.ops.object.select_all(action='SELECT')
    print("==========================================================")
    print("Successfully generated 4 Low-Poly Models for Unity!")
    print("Props created: Explosive Barrel, Supply Crate, Ammo Box, Kitchen Blender")
    
    # Auto-export to Unity Assets/Models directory
    import os
    export_dir = "/Users/mihir/Desktop/Counter_Boom/Assets/Models"
    os.makedirs(export_dir, exist_ok=True)
    
    fbx_path = os.path.join(export_dir, "GameProps.fbx")
    obj_path = os.path.join(export_dir, "GameProps.obj")
    
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

    print("==========================================================")
