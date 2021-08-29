public static class LookupTables
{
    public static float[] GetTables(int resolution)
    {
        int res1 = resolution;
        int res2 = resolution * resolution;

        return new float[8 + 24 + 25 + 27 + 1] {
                //Coordinates for drawing quads
                0, //o
                res2, //x
                res1, //y
                1, //z
                res2 + res1, //xy
                res2 + 1, //xz
                res1 + 1, //yz
                res2 + res1 + 1, //xyz
                //Indexes for each face for the above coordinates
                0, 2, 4, 1, //z face up
                7, 6, 3, 5, //z face down
                0, 1, 5, 3, //y face down
                7, 4, 2, 6, //y face up
                0, 3, 6, 2, //x face down
                7, 5, 1, 4, //x face up
                //Smoothing values for offsetting verticies depending on the surrounding geometry
                0,      0.33f,  0.33f,  0,      0,
                -0.33f, 0,      0,      0,      0,
                -0.33f, 0,      0,      0,      -0.33f,
                0,      0,      0,      0,      -0.33f,
                0,      0,      0.33f,  0.33f,  0,
                //Coordinates for adjacent voxels
                -res2 + -res1 + -1,
                -res2 + -res1,
                -res2 + -res1 + 1,
                -res2 + -1,
                -res2,
                -res2 + 1,
                -res2 + res1 + -1,
                -res2 + res1,
                -res2 + res1 + 1,
                -res1 + -1,
                -res1,
                -res1 + 1,
                -1,
                0,
                1,
                res1 + -1,
                res1,
                res1 + 1,
                res2 + -res1 + -1,
                res2 + -res1,
                res2 + -res1 + 1,
                res2 + -1,
                res2,
                res2 + 1,
                res2 + res1 + -1,
                res2 + res1,
                res2 + res1 + 1,
                //Refreshed flag
                0
        };
    }
}
