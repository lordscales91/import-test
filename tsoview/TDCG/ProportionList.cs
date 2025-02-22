using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CSScriptLibrary;

namespace TDCG
{
    /// <summary>
    /// 体型スクリプトのリストを扱います。
    /// </summary>
public class ProportionList
{
    static readonly ProportionList instance = new ProportionList();

    // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static ProportionList()
    {
    }

    ProportionList()
    {
    }

    /// <summary>
    /// 体型リスト
    /// </summary>
    public static ProportionList Instance
    {
        get
        {
            return instance;
        }
    }
    
    /// <summary>
    /// 体型スクリプトのリスト
    /// </summary>
    public List<IProportion> items = new List<IProportion>();

    /// <summary>
    /// 体型スクリプトフォルダのパスを得ます。
    /// </summary>
    /// <returns>体型スクリプトフォルダのパス</returns>
    public static string GetProportionPath()
    {
        return Path.Combine(Application.StartupPath, @"Proportion");
    }

    /// <summary>
    /// 体型スクリプトを読み込みます。
    /// 2回目は読み込みません。
    /// </summary>
    public void Load()
    {
        if (items.Count != 0)
            return;

        string proportion_path = GetProportionPath();
        if (! Directory.Exists(proportion_path))
            return;

        string[] script_files = Directory.GetFiles(proportion_path, "*.cs");
        foreach (string script_file in script_files)
        {
            string assembly_file = Path.GetTempFileName();
            string class_name = "TDCG.Proportion." + Path.GetFileNameWithoutExtension(script_file);
            var script = CSScript.Load(script_file, assembly_file, true, null).CreateInstance(class_name).AlignToInterface<IProportion>();
            items.Add(script);
        }
    }
}
}
