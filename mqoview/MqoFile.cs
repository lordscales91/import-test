using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using mqoview.Extensions;

namespace mqoview
{
    using BYTE  = Byte;
    using WORD  = UInt16;
    using DWORD = UInt32;
    using LONG  = Int32;

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    public struct TARGA_HEADER
    {
	    public BYTE     id;
	    public BYTE		colormap;
	    public BYTE		imagetype;
	    public BYTE		unknown0;
	    public BYTE		unknown1;
	    public BYTE		unknown2;
	    public BYTE		unknown3;
	    public BYTE		unknown4;
	    public WORD		x;
	    public WORD		y;
	    public WORD		width;
	    public WORD		height;
	    public BYTE		depth;
	    public BYTE		type;
    };

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct BITMAPFILEHEADER
    {
        public WORD    bfType;
        public DWORD   bfSize;
        public WORD    bfReserved1;
        public WORD    bfReserved2;
        public DWORD   bfOffBits;
    }

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct BITMAPINFOHEADER
    {
        public DWORD      biSize;
        public LONG       biWidth;
        public LONG       biHeight;
        public WORD       biPlanes;
        public WORD       biBitCount;
        public DWORD      biCompression;
        public DWORD      biSizeImage;
        public LONG       biXPelsPerMeter;
        public LONG       biYPelsPerMeter;
        public DWORD      biClrUsed;
        public DWORD      biClrImportant;
    }

    public class MqoFile : IDisposable
    {
        private delegate bool SectionHandler(string[] tokens);

        public static char[] delimiters = new char[] { ' ', '\t' };
        public static char[] delimiters2 = new char[] { ' ', '\t', '(', ')' };

        private string file;
        private StreamReader sr;
        private MqoScene scene;
        private List<MqoMaterial> materials;
        private List<MqoObject> objects = new List<MqoObject>();
        private List<MqoTexture> textures = new List<MqoTexture>();
        private MqoObject current;

        public MqoScene Scene { get { return scene; } }
        public List<MqoMaterial> Materials { get { return materials; } }
        public List<MqoObject> Objects { get { return objects; } }
        public List<MqoTexture> Textures { get { return textures; } }

        public void Load(string file)
        {
            using (FileStream fs = File.OpenRead(file))
            {
                this.file = file;
                sr = new StreamReader(fs, Encoding.Default);
                ReadAll();
            }
        }

        public void Dump()
        {
            System.Diagnostics.Debug.WriteLine(file);
            System.Diagnostics.Debug.WriteLine(scene);

            foreach (MqoMaterial i in materials)
                System.Diagnostics.Debug.WriteLine(i);

            foreach (MqoObject i in objects)
                System.Diagnostics.Debug.WriteLine(i);
        }

        public void ReadAll()
        {
            DoRead(SectionRoot);
        }

        private void DoRead(SectionHandler h)
        {
            for (int no = 1; ; ++no)
            {
                string line = sr.ReadLine();

                if (line == null)
                    break;

                line = line.Trim();
                string[] tokens = line.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

                try
                {
                    if (tokens.Length == 0)
                        continue;

                    if (!h(tokens))
                        break;
                }
                catch (Exception e)
                {
                    string msg = string.Format("File format error: {0} \"{1}\"", no, line);
                    throw new Exception(msg, e);
                }
            }
        }

        public void Error(string[] tokens)
        {
            throw new Exception("File Format Error: \"" + string.Concat(tokens) + "\"");
        }

        public static Vector3 ParseColor3(string[] t, int begin)
        {
            return new Vector3(
                float.Parse(t[begin + 0]),
                float.Parse(t[begin + 1]),
                float.Parse(t[begin + 2]));
        }

        public static Vector2 ParsePoint2(string[] t, int begin)
        {
            return new Vector2(
                float.Parse(t[begin + 0]),
                float.Parse(t[begin + 1]));
        }

        public static Vector3 ParsePoint3(string[] t, int begin)
        {
            return new Vector3(
                float.Parse(t[begin + 0]),
                float.Parse(t[begin + 1]),
                float.Parse(t[begin + 2]));
        }

        private bool SectionRoot(string[] tokens)
        {
            switch (tokens[0].ToLower())
            {
                case "metasequoia": ParseMetasequoia(tokens); return true;
                case "format": ParseFormat(tokens); return true;
                case "scene": ParseScene(tokens); return true;
                case "material": ParseMaterial(tokens); return true;
                case "object": ParseObject(tokens); return true;
                case "eof": return false;
                default: return true;
            }
        }

        private bool SectionScene(string[] tokens)
        {
            scene = new MqoScene();

            switch (tokens[0].ToLower())
            {
                case "pos": scene.pos = ParsePoint3(tokens, 1); return true;
                case "lookat": scene.lookat = ParsePoint3(tokens, 1); return true;
                case "head": scene.head = float.Parse(tokens[1]); return true;
                case "pich": scene.pich = float.Parse(tokens[1]); return true;
                case "ortho": scene.ortho = float.Parse(tokens[1]); return true;
                case "zoom2": scene.zoom2 = float.Parse(tokens[1]); return true;
                case "amb": scene.amb = ParseColor3(tokens, 1); return true;
                case "}": return false;
                default: return true;
            }
        }

        private bool SectionMaterial(string[] tokens)
        {
            if (tokens[0] == "}")
                return false;

            StringBuilder sb = new StringBuilder();

            foreach (string i in tokens)
                sb.Append(' ').Append(i);

            string line = sb.ToString().Trim();
            MqoMaterial m = new MqoMaterial(tokens[0].Trim('"'));
            tokens = line.Split(delimiters2, StringSplitOptions.RemoveEmptyEntries);
            materials.Add(m);

            for (int i = 1; i < tokens.Length; ++i)
            {
                string t = tokens[i];

                switch (tokens[i].ToLower())
                {
                    case "shader": m.shader_id = int.Parse(tokens[++i]); break;
                    case "col": m.col = ParseColor3(tokens, i + 1); i += 4; break;
                    case "dif": m.dif = float.Parse(tokens[++i]); break;
                    case "amb": m.amb = float.Parse(tokens[++i]); break;
                    case "emi": m.emi = float.Parse(tokens[++i]); break;
                    case "spc": m.spc = float.Parse(tokens[++i]); break;
                    case "power": m.power = float.Parse(tokens[++i]); break;
                    case "tex": m.tex = tokens[++i].Trim('\"'); break;
                }
            }

            return true;
        }

        private bool SectionObject(string[] tokens)
        {
            switch (tokens[0].ToLower())
            {
                case "visible": current.visible = int.Parse(tokens[1]); return true;
                case "locking": current.locking = int.Parse(tokens[1]); return true;
                case "shading": current.shading = int.Parse(tokens[1]); return true;
                case "facet": current.facet = float.Parse(tokens[1]); return true;
                case "color": current.color = ParseColor3(tokens, 1); return true;
                case "color_type": current.color_type = int.Parse(tokens[1]); return true;
                case "vertex": ParseVertex(tokens); return true;
                case "face": ParseFace(tokens); return true;
                case "}": return false;
                default: return true;
            }
        }

        private bool SectionVertex(string[] tokens)
        {
            if (tokens[0] == "}")
                return false;
            UVertex v = new UVertex();
            v.position = ParsePoint3(tokens, 0);
            current.vertices.Add(v);

            return true;
        }

        private bool SectionFace(string[] tokens)
        {
            if (tokens[0] == "}")
                return false;

            if (3 != int.Parse(tokens[0]))
                return true;

            StringBuilder sb = new StringBuilder();

            foreach (string i in tokens)
                sb.Append(' ').Append(i);

            string line = sb.ToString().Trim();
            MqoFace f = new MqoFace();
            tokens = line.Split(delimiters2, StringSplitOptions.RemoveEmptyEntries);
            current.faces.Add(f);

            for (int i = 1; i < tokens.Length; ++i)
            {
                switch (tokens[i].ToLower())
                {
                    case "v":
                        f.a = ushort.Parse(tokens[++i]);
                        f.b = ushort.Parse(tokens[++i]);
                        f.c = ushort.Parse(tokens[++i]);
                        break;
                    case "m":
                        f.mtl = ushort.Parse(tokens[++i]);
                        break;
                    case "uv":
                        f.ta = ParsePoint2(tokens, i + 1); i += 2;
                        f.tb = ParsePoint2(tokens, i + 1); i += 2;
                        f.tc = ParsePoint2(tokens, i + 1); i += 2;
                        break;
                }
            }

            return true;
        }

        //----- Root elements ----------------------------------------------
        private void ParseMetasequoia(string[] tokens)
        {
            if (tokens[1].ToLower() != "document") Error(tokens);
        }

        private void ParseFormat(string[] tokens)
        {
            if (tokens[1].ToLower() != "text") Error(tokens);
            if (tokens[2].ToLower() != "ver") Error(tokens);
            if (tokens[3].ToLower() != "1.0") Error(tokens);
        }

        private void ParseScene(string[] tokens)
        {
            if (tokens[1].ToLower() != "{") Error(tokens);

            DoRead(SectionScene);
        }

        private void ParseMaterial(string[] tokens)
        {
            if (tokens[2].ToLower() != "{") Error(tokens);

            materials = new List<MqoMaterial>(int.Parse(tokens[1]));
            DoRead(SectionMaterial);
        }

        private void ParseObject(string[] tokens)
        {
            if (tokens[2].ToLower() != "{") Error(tokens);

            current = new MqoObject(tokens[1].Trim('"'));
            objects.Add(current);
            DoRead(SectionObject);
        }

        private void ParseVertex(string[] tokens)
        {
            if (tokens[2].ToLower() != "{") Error(tokens);

            current.vertices = new List<UVertex>(int.Parse(tokens[1]));
            DoRead(SectionVertex);
        }

        private void ParseFace(string[] tokens)
        {
            if (tokens[2].ToLower() != "{") Error(tokens);

            current.faces = new List<MqoFace>(int.Parse(tokens[1]));
            DoRead(SectionFace);
        }

        internal Device device;
        internal Effect effect;

        EffectHandle[] techniques;
        internal Dictionary<string, EffectHandle> techmap;

        private EffectHandle handle_ShadeTex_texture;
        private EffectHandle handle_ColorTex_texture;
        internal Dictionary<string, MqoTexture> texmap;

        private EffectHandle handle_LightDir;
        private EffectHandle handle_LightDirForced;
        private EffectHandle handle_UVSCR;

        /// <summary>
        /// 指定device上で開きます。
        /// </summary>
        /// <param name="device">device</param>
        /// <param name="effect">effect</param>
        public void Open(Device device, Effect effect)
        {
            this.device = device;
            this.effect = effect;

            ImportInfo info = ImportInfo.Load(Path.ChangeExtension(this.file, ".xml"));

            string dir = Path.GetDirectoryName(this.file);
            foreach (ImportTextureInfo tex_info in info.textures)
            {
                MqoTexture tex = new MqoTexture();
                tex.Name = tex_info.Name;
                string tex_path = Path.Combine(dir, tex_info.File);
                tex.Load(tex_path);
                tex.Open(device);

                textures.Add(tex);
            }

            texmap = new Dictionary<string, MqoTexture>();
            foreach (MqoTexture tex in textures)
            {
                texmap[tex.name] = tex;
            }

            foreach (MqoMaterial mtl in Materials)
            {
                string sub_script_path = Path.Combine(dir, mtl.name);
                mtl.Load(sub_script_path);
                mtl.GenerateShader();
            }

            handle_ShadeTex_texture = effect.GetParameter(null, "ShadeTex_texture");
            handle_ColorTex_texture = effect.GetParameter(null, "ColorTex_texture");

            handle_LightDir = effect.GetParameter(null, "LightDir");
            handle_LightDirForced = effect.GetParameter(null, "LightDirForced");
            handle_UVSCR = effect.GetParameter(null, "UVSCR");

            techmap = new Dictionary<string, EffectHandle>();

            int ntech = effect.Description.Techniques;
            techniques = new EffectHandle[ntech];

            //Console.WriteLine("Techniques:");

            for (int i = 0; i < ntech; i++)
            {
                techniques[i] = effect.GetTechnique(i);
                string tech_name = effect.GetTechniqueDescription(techniques[i]).Name;
                techmap[tech_name] = techniques[i];

                //Console.WriteLine(i + " " + tech_name);
            }

            foreach (MqoObject obj in Objects)
            {
                obj.WriteBuffer(device);
            }
        }

        internal Shader current_shader = null;
        internal Vector3 lightDir = new Vector3(0.0f, 0.0f, -1.0f);

        /// <summary>
        /// 光源方向ベクトルを得ます。
        /// </summary>
        /// <returns></returns>
        public Vector4 LightDirForced()
        {
            return new Vector4(lightDir.X, lightDir.Y, lightDir.Z, 0.0f);
        }

        /// <summary>
        /// UVSCR値を得ます。
        /// </summary>
        /// <returns></returns>
        public Vector4 UVSCR()
        {
            float x = Environment.TickCount * 0.000002f;
            return new Vector4(x, 0.0f, 0.0f, 0.0f);
        }

        /// <summary>
        /// レンダリング開始時に呼びます。
        /// </summary>
        public void BeginRender()
        {
            current_shader = null;
        }

        /// <summary>
        /// シェーダ設定を切り替えます。
        /// </summary>
        /// <param name="shader">シェーダ設定</param>
        public void SwitchShader(Shader shader)
        {
            if (shader == current_shader)
                return;
            current_shader = shader;

            if (! techmap.ContainsKey(shader.technique))
            {
                Console.WriteLine("Error: shader technique not found. " + shader.technique);
                return;
            }

            foreach (ShaderParameter p in shader.shader_parameters)
            {
                if (p.system_p)
                    continue;

                switch (p.type)
                {
                case ShaderParameter.Type.String:
                    effect.SetValue(p.name, p.GetString());
                    break;
                case ShaderParameter.Type.Float:
                case ShaderParameter.Type.Float3:
                case ShaderParameter.Type.Float4:
                    effect.SetValue(p.name, new float[]{ p.F1, p.F2, p.F3, p.F4 });
                    break;
                    /*
                case ShaderParameter.Type.Texture:
                    effect.SetValue(p.name, p.GetTexture());
                    break;
                    */
                }
            }
            effect.SetValue(handle_LightDir, shader.LightDir);
            effect.SetValue(handle_LightDirForced, LightDirForced());
            //effect.SetValue(handle_UVSCR, UVSCR());

            MqoTexture shadeTex;
            if (shader.shadeTex != null && texmap.TryGetValue(shader.ShadeTexName, out shadeTex))
                effect.SetValue(handle_ShadeTex_texture, shadeTex.tex);

            MqoTexture colorTex;
            if (shader.colorTex != null && texmap.TryGetValue(shader.ColorTexName, out colorTex))
                effect.SetValue(handle_ColorTex_texture, colorTex.tex);

            effect.Technique = techmap[shader.technique];
            effect.ValidateTechnique(effect.Technique);
        }

        /// <summary>
        /// レンダリング終了時に呼びます。
        /// </summary>
        public void EndRender()
        {
            current_shader = null;
        }

        public void Dispose()
        {
            foreach (MqoObject obj in objects)
                obj.Dispose();
            foreach (MqoTexture tex in textures)
                tex.Dispose();
        }
    }

    public class MqoScene
    {
        public Vector3 pos;
        public Vector3 lookat;
        public float head;
        public float pich;
        public float ortho;
        public float zoom2;
        public Vector3 amb;

        public override string ToString()
        {
            return (new StringBuilder(256))
                .Append(" pos: ").Append(pos)
                .Append(" lookat: ").Append(lookat)
                .Append(" head: ").Append(head)
                .Append(" pich: ").Append(pich)
                .Append(" ortho: ").Append(ortho)
                .Append(" zoom2: ").Append(zoom2)
                .Append(" amb: ").Append(amb)
                .ToString();
        }
    }

    public class MqoMaterial
    {
        public string name;
        public int shader_id;
        public Vector3 col;
        public float dif;
        public float amb;
        public float emi;
        public float spc;
        public float power;
        public string tex;
        /// <summary>
        /// テキスト行配列
        /// </summary>
        public string[] lines;
        /// <summary>
        /// シェーダ設定
        /// </summary>
        public Shader shader = null;

        public MqoMaterial() { }
        public MqoMaterial(string name) { this.name = name; }

        public override string ToString()
        {
            return (new StringBuilder(256))
                .Append(" shader: ").Append(shader_id)
                .Append(" col: ").Append(col)
                .Append(" dif: ").Append(dif)
                .Append(" amb: ").Append(amb)
                .Append(" emi: ").Append(emi)
                .Append(" spc: ").Append(spc)
                .Append(" power: ").Append(power)
                .Append(" tex: ").Append(tex)
                .Append(" name: ").Append(name)
                .ToString();
        }

        /// <summary>
        /// サブスクリプトを読み込みます。
        /// </summary>
        public void Load(string source_file)
        {
            this.lines = File.ReadAllLines(source_file);
        }

        /// <summary>
        /// シェーダ設定を生成します。
        /// </summary>
        public void GenerateShader()
        {
            this.shader = new Shader();
            this.shader.Load(this.lines);
        }
    }

    public class MqoObject : IDisposable
    {
        public string name;
        public int visible;
        public int locking;
        public int shading;
        public float facet;
        public Vector3 color;
        public int color_type;
        public List<UVertex> vertices;
        public List<MqoFace> faces;
        public MqoAttributeTable at = new MqoAttributeTable();

        public Mesh dm = null;

        public MqoObject() { }
        public MqoObject(string n) { name = n; }

        public override string ToString()
        {
            return (new StringBuilder(256))
                .Append(" visible: ").Append(visible)
                .Append(" locking: ").Append(locking)
                .Append(" shading: ").Append(shading)
                .Append(" facet: ").Append(facet)
                .Append(" color: ").Append(color)
                .Append(" color_type: ").Append(color_type)
                .Append(" vertices: ").Append(vertices.Count)
                .Append(" faces: ").Append(faces.Count)
                .Append(" name: ").Append(name)
                .ToString();
        }

        static VertexElement[] ve = new VertexElement[]
        {
            new VertexElement(0,  0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
            new VertexElement(0, 12, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 3),
            new VertexElement(0, 28, DeclarationType.Ubyte4, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 4),
            new VertexElement(0, 32, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
            new VertexElement(0, 44, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
                VertexElement.VertexDeclarationEnd
        };

        /// <summary>
        /// 頂点をDirect3Dバッファに書き込みます。
        /// </summary>
        /// <param name="device">device</param>
        public void WriteBuffer(Device device)
        {
            Vector3[] normals = new Vector3[vertices.Count];
            foreach (MqoFace face in faces)
            {
                Vector3 v1 = vertices[face.a].position - vertices[face.c].position;
                Vector3 v2 = vertices[face.b].position - vertices[face.c].position;
                Vector3 n = Vector3.Normalize(Vector3.Cross(v2, v1));
                normals[face.a] += n;
                normals[face.b] += n;
                normals[face.c] += n;
            }
            {
                int i = 0;
                foreach (UVertex v in vertices)
                {
                    v.normal = Vector3.Normalize(normals[i]);
                    i++;
                }
            }

            UVertexHeap heap = new UVertexHeap();
            List<ushort> indices = new List<ushort>(faces.Count * 3);
            {
                int face_len = 0;
                MqoAttributeRange ar = at.Start(face_len, faces[0].mtl);
                foreach (MqoFace face in faces)
                {
                    if (face.mtl != ar.mtl)
                    {
                        face_len += indices.Count / 3;
                        ar = at.Next(face_len, face.mtl);
                    }
                    UVertex a = new UVertex(vertices[face.a].position, vertices[face.a].normal, face.ta.X, 1 - face.ta.Y, face.mtl);
                    UVertex b = new UVertex(vertices[face.b].position, vertices[face.b].normal, face.tb.X, 1 - face.tb.Y, face.mtl);
                    UVertex c = new UVertex(vertices[face.c].position, vertices[face.c].normal, face.tc.X, 1 - face.tc.Y, face.mtl);
                    indices.Add(heap.Add(a));
                    indices.Add(heap.Add(c));
                    indices.Add(heap.Add(b));
                }
                {
                    face_len += indices.Count / 3;
                    at.Finish(face_len);
                    ar = null;
                }
            }

            if (dm != null)
            {
                dm.Dispose();
                dm = null;
            }
            dm = new Mesh(at.FaceCount, heap.Count, MeshFlags.Managed | MeshFlags.WriteOnly, ve, device);

            //
            // rewrite vertex buffer
            //
            {
                GraphicsStream gs = dm.LockVertexBuffer(LockFlags.None);
                {
                    foreach (UVertex v in heap.ary)
                    {
                        gs.Write(v.position);
                        gs.Write(1.0f);
                        gs.Write(0.0f);
                        gs.Write(0.0f);
                        gs.Write(0.0f);
                        gs.Write(0);
                        gs.Write(v.normal);
                        gs.Write(v.u);
                        gs.Write(v.v);
                    }
                }
                dm.UnlockVertexBuffer();
            }

            //
            // rewrite index buffer
            //
            {
                GraphicsStream gs = dm.LockIndexBuffer(LockFlags.None);
                {
                    foreach (ushort idx in indices)
                    {
                        gs.Write(idx);
                    }
                }
                dm.UnlockIndexBuffer();
            }

            //
            // rewrite attribute buffer
            //
            {
                int[] attribBuffer = dm.LockAttributeBufferArray(LockFlags.None);
                foreach (MqoAttributeRange ar in at.Ranges)
                {
                    for (int i = 0; i < ar.FaceCount; i++)
                        attribBuffer[ar.FaceStart + i] = ar.AttributeId;
                }
                dm.UnlockAttributeBuffer(attribBuffer);

                dm.SetAttributeTable(at.GenerateAttributeTable(0, heap.Count));
            }
        }

        public void Dispose()
        {
            if (dm != null)
                dm.Dispose();
        }
    }

    public class MqoAttributeRange
    {
        public ushort mtl;
        public int AttributeId;
        public int FaceStart;
        public int FaceCount;
    }

    public class MqoAttributeTable
    {
        MqoAttributeRange ar = null;
        List<MqoAttributeRange> at = new List<MqoAttributeRange>();
        public List<MqoAttributeRange> Ranges { get { return at; } }
        public int FaceCount;

        public MqoAttributeRange Start(int len, ushort mtl)
        {
            ar = new MqoAttributeRange();
            ar.mtl = mtl;
            ar.AttributeId = at.Count;
            ar.FaceStart = len;
            at.Add(ar);
            return ar;
        }

        public MqoAttributeRange Next(int len, ushort mtl)
        {
            ar.FaceCount = len - FaceCount;
            FaceCount += ar.FaceCount;
            ar = new MqoAttributeRange();
            ar.mtl = mtl;
            ar.AttributeId = at.Count;
            ar.FaceStart = len;
            at.Add(ar);
            return ar;
        }

        public void Finish(int len)
        {
            ar.FaceCount = len - FaceCount;
            FaceCount += ar.FaceCount;
            ar = null;
        }

        public AttributeRange[] GenerateAttributeTable(int vertex_start, int vertex_count)
        {
            AttributeRange[] da = new AttributeRange[at.Count];
            for (int i = 0; i < at.Count; i++)
            {
                MqoAttributeRange ar = at[i];
                da[i].AttributeId = ar.AttributeId;
                da[i].FaceStart = ar.FaceStart;
                da[i].FaceCount = ar.FaceCount;
                da[i].VertexStart = vertex_start;
                da[i].VertexCount = vertex_count;
            }
            return da;
        }
    }

    public class MqoFace
    {
        public ushort a, b, c, mtl;
        public Vector2 ta, tb, tc;

        public MqoFace()
        {
        }

        public MqoFace(ushort a, ushort b, ushort c, ushort mtl, Vector2 ta, Vector2 tb, Vector2 tc)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.mtl = mtl;
            this.ta = ta;
            this.tb = tb;
            this.tc = tc;
        }

        public override string ToString()
        {
            return (new StringBuilder(256))
                .Append("v: ").Append(a).Append(" ").Append(b).Append(" ").Append(c)
                .Append(" mtl: ").Append(mtl)
                .Append(" uv: ").Append(ta).Append(" ").Append(tb).Append(" ").Append(tc)
                .ToString();
        }
    }

    public class UVertexHeap
    {
        public Dictionary<UVertex, ushort> map = new Dictionary<UVertex, ushort>();
        public List<UVertex> ary = new List<UVertex>();

        public void Clear()
        {
            map.Clear();
            ary.Clear();
        }

        public ushort Add(UVertex v)
        {
            ushort n;

            if (map.TryGetValue(v, out n))
                return n;

            n = (ushort)ary.Count;
            map.Add(v, n);
            ary.Add(v);
            return n;
        }

        public int Count { get { return ary.Count; } }
        public ushort this[UVertex index] { get { return map[index]; } }
        public UVertex this[int index] { get { return ary[index]; } }
    }

    public class UVertex : IEquatable<UVertex>
    {
        public Vector3 position;
        public Vector3 normal;
        public float u, v;
        public int mtl;

        public UVertex()
        {
        }

        public UVertex(Vector3 position, Vector3 normal, float u, float v, int mtl)
        {
            this.position = position;
            this.normal = normal;
            this.u = u;
            this.v = v;
            this.mtl = mtl;
        }

        public bool Equals(UVertex other)
        {
            if (other == null)
                return base.Equals(other);

            return position == other.position && normal == other.normal && u == other.u && v == other.v && mtl == other.mtl;
        }

        public override int GetHashCode()
        {
            return position.GetHashCode() ^ normal.GetHashCode() ^ u.GetHashCode() ^ v.GetHashCode() ^ mtl.GetHashCode();
        }
    }

    /// <summary>
    /// テクスチャ
    /// </summary>
    public class MqoTexture : IDisposable
    {
        /// <summary>
        /// 名称
        /// </summary>
        internal string name;
        /// <summary>
        /// ファイル名
        /// </summary>
        internal string file;
        /// <summary>
        /// 幅
        /// </summary>
        public int width;
        /// <summary>
        /// 高さ
        /// </summary>
        public int height;
        /// <summary>
        /// 色深度
        /// </summary>
        public int depth;
        /// <summary>
        /// ビットマップ配列
        /// </summary>
        public byte[] data;

        internal Texture tex;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get { return name; } set { name = value; } }
        /// <summary>
        /// ファイル名
        /// </summary>
        public string FileName { get { return file; } set { file = value; } }

        /// <summary>
        /// テクスチャを読み込みます。
        /// </summary>
        public void Load(string source_file)
        {
            using (FileStream stream = File.OpenRead(source_file))
            {
                this.file = "\"" + Path.GetFileName(source_file) + "\"";
                string ext = Path.GetExtension(source_file).ToLower();
                switch (ext)
                {
                    case ".tga":
                        LoadFromTga(stream);
                        break;
                    case ".bmp":
                        LoadFromBmp(stream);
                        break;
                }
            }
        }

        static readonly int sizeof_tga_header = Marshal.SizeOf(typeof(TARGA_HEADER));

        /// <summary>
        /// テクスチャを読み込みます。
        /// </summary>
        public void LoadFromTga(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            TARGA_HEADER header;

            IntPtr header_ptr = Marshal.AllocHGlobal(sizeof_tga_header);
            Marshal.Copy(br.ReadBytes(sizeof_tga_header), 0, header_ptr, sizeof_tga_header);
            header = (TARGA_HEADER)Marshal.PtrToStructure(header_ptr, typeof(TARGA_HEADER));

            if (header.imagetype != 0x02)
                throw new Exception("Invalid imagetype: " + file);
            if (header.depth != 24 && header.depth != 32)
                throw new Exception("Invalid depth: " + file);

            this.width = header.width;
            this.height = header.height;
            this.depth = header.depth / 8;
            this.data = br.ReadBytes( this.width * this.height * this.depth );
        }

        static readonly int sizeof_bfh = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
        static readonly int sizeof_bih = Marshal.SizeOf(typeof(BITMAPINFOHEADER));

        /// <summary>
        /// テクスチャを読み込みます。
        /// </summary>
        public void LoadFromBmp(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            BITMAPFILEHEADER bfh;
            BITMAPINFOHEADER bih;

            IntPtr bfh_ptr = Marshal.AllocHGlobal(sizeof_bfh);
            Marshal.Copy(br.ReadBytes(sizeof_bfh), 0, bfh_ptr, sizeof_bfh);
            bfh = (BITMAPFILEHEADER)Marshal.PtrToStructure(bfh_ptr, typeof(BITMAPFILEHEADER));

            IntPtr bih_ptr = Marshal.AllocHGlobal(sizeof_bih);
            Marshal.Copy(br.ReadBytes(sizeof_bih), 0, bih_ptr, sizeof_bih);
            bih = (BITMAPINFOHEADER)Marshal.PtrToStructure(bih_ptr, typeof(BITMAPINFOHEADER));

            if (bfh.bfType != 0x4D42)
                throw new Exception("Invalid imagetype: " + file);
            if (bih.biBitCount != 24 && bih.biBitCount != 32)
                throw new Exception("Invalid depth: " + file);

            this.width = bih.biWidth;
            this.height = bih.biHeight;
            this.depth = bih.biBitCount / 8;
            this.data = br.ReadBytes( this.width * this.height * this.depth );
        }

        /// <summary>
        /// テクスチャを読み込みます。
        /// </summary>
        public void Read(BinaryReader reader)
        {
            this.name = reader.ReadCString();
            this.file = reader.ReadCString();
            this.width = reader.ReadInt32();
            this.height = reader.ReadInt32();
            this.depth = reader.ReadInt32();
            this.data = reader.ReadBytes( this.width * this.height * this.depth );

            for(int j = 0; j < this.data.Length; j += 4)
            {
                byte tmp = this.data[j+2];
                this.data[j+2] = this.data[j+0];
                this.data[j+0] = tmp;
            }
        }

        /// <summary>
        /// 指定deviceで開きます。
        /// </summary>
        /// <param name="device">device</param>
        public void Open(Device device)
        {
            if (file.Trim('"') == "")
                return;
            MemoryStream ms = new MemoryStream();
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                {
                    bw.Write((byte)'B');
                    bw.Write((byte)'M');
                    bw.Write((int)(54 + data.Length));
                    bw.Write((int)0);
                    bw.Write((int)54);
                    bw.Write((int)40);
                    bw.Write((int)width);
                    bw.Write((int)height);
                    bw.Write((short)1);
                    bw.Write((short)(depth*8));
                    bw.Write((int)0);
                    bw.Write((int)data.Length);
                    bw.Write((int)0);
                    bw.Write((int)0);
                    bw.Write((int)0);
                    bw.Write((int)0);
                }

                int count = width * depth;
                int index = width * height * depth - count;
                for (int y = 0; y < height; y++)
                {
                    bw.Write(data, index, count);
                    index -= count;
                }
                bw.Flush();

                ms.Seek(0, SeekOrigin.Begin);
                tex = TextureLoader.FromStream(device, ms);
            }
        }

        /// <summary>
        /// Direct3Dテクスチャを破棄します。
        /// </summary>
        public void Dispose()
        {
            if (tex != null)
                tex.Dispose();
        }
    }
}
