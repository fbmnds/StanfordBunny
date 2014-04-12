module MainApp

open System.Windows
open System.Windows.Media.Media3D


let (|Int|_|) (s: string) =
        let mutable n = 0
        if System.Int32.TryParse(s, &n) then Some n else None

/// switch to "en-GB" culture for reading floats with decimal point
/// on a "de-DE" system expecting to parse floats with decimal comma
let (|Float|_|) (s: string) =
        let mutable x = 0.0
        let style = System.Globalization.NumberStyles.Float
        let culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-GB")
        if System.Double.TryParse(s, style, culture, &x) then Some x else None

let split (s: string) =
        s.Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)

/// TODO: find where to configure __SOURCE_DIRECTORY__
let getReader() =
        let file = @"C:\Users\Friedrich\projects\StanfordBunny\bun_zipper.ply"
        new System.IO.StreamReader(file)

let position, index =
        use reader = getReader()
        let position, index = Point3DCollection(), Media.Int32Collection()
        while not reader.EndOfStream do
          match reader.ReadLine() |> split with
          | [|"3"; Int i; Int j; Int k|] -> Seq.iter index.Add [i; j; k]
          /// BUG in original code: 
          /// intention is to match, if and only if the line contains parsed floats
          /// but ply file header lines also match because Float x is an option value
          /// hence, Point3D constructor will be called with 3 option values
          /// which raises an exception.
          /// WORKAROUND: remove header lines from ply file 
          | [|Float x; Float y; Float z; _; _|] -> position.Add(Point3D(x, y, z))
          | _ -> ()
        position, index

let zero = Vector3D(0.0, 0.0, 0.0)

let up = Vector3D(0.0, 1.0, 0.0)

let p2v (p: Point3D) = Vector3D (p.X, p.Y, p.Z)

let normal =
        let normal = Array.create position.Count zero
        /// BUG resolved: index.Count-1 -> index.Count-2
        for n in 0 .. 3 .. index.Count-2 do
          let mutable i, j, k = index.[n], index.[n+1], index.[n+2]
          let u, v, w = position.[i], position.[j], position.[k]
          let mutable n = Vector3D.CrossProduct(v - u, w - u)
          n.Normalize()
          for i in [i; j; k] do
             /// the sum of normals is not normalized itself
             normal.[i] <- normal.[i] + n 
        /// final normalization omitted
        Vector3DCollection normal

let directionalLight (color, direction) =
        DirectionalLight(Color=color, Direction=direction) :> Model3D

let lights =
        [ Media.Colors.Red, Vector3D(-1.0, 0.0, 1.0);
          Media.Colors.Green, Vector3D(-1.0, -1.0, 1.0);
          Media.Colors.Blue, Vector3D(0.0, -1.0, 1.0) ]
        |> List.map directionalLight

let camera =
        PerspectiveCamera(Position=Point3D(0.3, 0.40, -0.5),
                          LookDirection=Vector3D(-0.3, -0.3, 0.5),
                          FieldOfView=35.0)

let mesh = MeshGeometry3D(Normals=normal, Positions=position, TriangleIndices=index)

let model =
        let material = DiffuseMaterial Media.Brushes.White
        GeometryModel3D(mesh, material) :> Model3D

let visual =
        let root = Model3DGroup(Children=Model3DCollection(model::lights))
        ModelVisual3D(Content=root)

let viewport3d = Controls.Viewport3D(Camera=camera)

let rotate axis angle =
        RotateTransform3D(AxisAngleRotation3D(axis, angle))

let loadWindow() =
    visual |> viewport3d.Children.Add |> ignore
    let time = System.Diagnostics.Stopwatch.StartNew()
    Media.CompositionTarget.Rendering.Add(fun _ -> model.Transform <- rotate up (30.0 * float time.Elapsed.TotalSeconds))
    /// scaffold main window
    Window(Content=viewport3d, Title="Stanford bunny")


[<System.STAThread>]
    (new Application()).Run(loadWindow()) |> ignore

