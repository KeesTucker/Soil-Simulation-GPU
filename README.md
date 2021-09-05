# Soil-Simulation-GPU
Inspiration taken from:
- Catlike Coding https://catlikecoding.com/unity/tutorials/marching-squares/
- Scrawk https://github.com/Scrawk/Marching-Cubes-On-The-GPU
- BorisTheBrave https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/
Big thanks to them! Without them I would have had no idea where to start.

Basic soil voxel phsyics are included such as gravity and "spread". 
The rendering technique is experimental and takes a lot of inspiration from Dual Contouring but uses a voxel density field instead of the derivative. 
It is NOT marching cubes. IT IS ALSO VERY WIP.
It produces much lower vert counts compared with marching cubes in exhange for slightly higher computational costs. 
It is also very good at rendering low resolution grids and making them appear smooth and rounded.
It joins all verts and averages the normals in the same pass as all the other mesh generation.
Mesh is dynamically sized so only the correct amount of triangles are rendered. This is accomplished with Append Buffers and DrawProceduralIndirect()
Mesh is drawn using DrawProceduralIndirect() and no data is passed between CPU and GPU, for colliders data is passed between, but it is done async.
There are two seperate shaders as DrawProceduralIndirectNow() renders the geometry and a seperate shader containing only a shadow pass is called in DrawProceduralIndirect().
Colliders are generated from box colliders.
