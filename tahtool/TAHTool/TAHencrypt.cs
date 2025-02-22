using System;
using System.Collections.Generic;
using System.IO;

namespace TAHTool
{
    class TAHancrypt
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string source_file = args[0];
                //encrypt to TAH archive
                if (Directory.Exists(source_file))
                {
                    //launch encrypt routine from here...
                    encrypt_archive(source_file + ".tah", source_file);
                }
            }
        }

        static int encrypt_archive(string file_path_name, string source_path)
        {
            //check if file already exists... if yes rename it
            try
            {
                if (File.Exists(file_path_name))
                {
                    File.Move(file_path_name, file_path_name + ".bak");
                }
            }
            catch (IOException)
            {
                System.Console.Out.WriteLine("Error: Could not rename existing TAH file.");
                return -1;
            }
            Encrypter encrypter = new Encrypter();

            //read in files from source path, do not compress them now.
            //全ディレクトリ名
            string[] directories = Directory.GetDirectories(source_path, "*", SearchOption.AllDirectories);

            {
                string dirname = source_path;
                string[] files = Directory.GetFiles(dirname);
                foreach (string file in files)
                    encrypter.Add(file);
            }
            for (int i = 0; i < directories.Length; i++)
            {
                string dirname = directories[i];
                string[] files = Directory.GetFiles(dirname);
                foreach (string file in files)
                    encrypter.Add(file);
            }

            encrypter.SourcePath = source_path;
            encrypter.GetFileEntryStream = delegate(string true_file_name)
            {
                return File.OpenRead(true_file_name);
            };
            encrypter.Save(file_path_name);
            return 0;
        }
    }
}
