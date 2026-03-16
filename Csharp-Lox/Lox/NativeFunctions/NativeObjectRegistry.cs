using System;
using System.Collections.Generic;

namespace Lox
{
    /// <summary>
    /// 用于将 .NET 对象注册到 Lox 环境的工具类
    /// </summary>
    internal static class NativeObjectRegistry
    {
        /// <summary>
        /// 将 .NET 对象注册到 Lox 全局环境
        /// </summary>
        /// <param name="interpreter">Lox 解释器</param>
        /// <param name="name">在 Lox 中的变量名</param>
        /// <param name="obj">要注册的 .NET 对象</param>
        public static void Register(Interpreter interpreter, string name, object obj)
        {
            if (obj == null)
            {
                interpreter.Globals.Define(name, null);
                return;
            }

            var nativeObj = new LoxNativeObject(obj);
            interpreter.Globals.Define(name, nativeObj);
        }

        /// <summary>
        /// 将 .NET 对象注册到 Lox 全局环境（扩展方法版本）
        /// </summary>
        public static void RegisterNative(this Interpreter interpreter, string name, object obj)
        {
            Register(interpreter, name, obj);
        }
    }

    /// <summary>
    /// 示例：创建一个可以暴露给 Lox 的数学工具类
    /// </summary>
    public class LoxMath
    {
        public double Pi => Math.PI;
        public double E => Math.E;

        public double Sin(double x) => Math.Sin(x);
        public double Cos(double x) => Math.Cos(x);
        public double Tan(double x) => Math.Tan(x);
        public double Sqrt(double x) => Math.Sqrt(x);
        public double Pow(double x, double y) => Math.Pow(x, y);
        public double Abs(double x) => Math.Abs(x);
        public double Floor(double x) => Math.Floor(x);
        public double Ceiling(double x) => Math.Ceiling(x);
        public double Round(double x) => Math.Round(x);
        public double Log(double x) => Math.Log(x);
        public double Log10(double x) => Math.Log10(x);
        public double Exp(double x) => Math.Exp(x);
        public double Min(double a, double b) => Math.Min(a, b);
        public double Max(double a, double b) => Math.Max(a, b);
    }

    /// <summary>
    /// 示例：创建一个可以暴露给 Lox 的字符串工具类
    /// </summary>
    public class LoxString
    {
        public double Length(string str) => str?.Length ?? 0;
        public string Substring(string str, double start, double length)
        {
            if (str == null) return "";
            int s = (int)start;
            int len = (int)length;
            if (s < 0) s = 0;
            if (s >= str.Length) return "";
            if (s + len > str.Length) len = str.Length - s;
            return str.Substring(s, len);
        }
        public string ToUpper(string str) => str?.ToUpper() ?? "";
        public string ToLower(string str) => str?.ToLower() ?? "";
        public bool Contains(string str, string value) => str?.Contains(value) ?? false;
        public double IndexOf(string str, string value) => str?.IndexOf(value) ?? -1;
        public string Replace(string str, string oldValue, string newValue) => str?.Replace(oldValue, newValue) ?? "";
        public string Trim(string str) => str?.Trim() ?? "";
        public bool StartsWith(string str, string value) => str?.StartsWith(value) ?? false;
        public bool EndsWith(string str, string value) => str?.EndsWith(value) ?? false;
        public string Concat(string a, string b) => (a ?? "") + (b ?? "");
    }

    /// <summary>
    /// 示例：创建一个可以暴露给 Lox 的列表/数组工具类
    /// </summary>
    public class LoxList
    {
        public List<object> Create() => new List<object>();

        public void Add(List<object> list, object item)
        {
            list?.Add(item);
        }

        public void RemoveAt(List<object> list, double index)
        {
            if (list != null && index >= 0 && index < list.Count)
            {
                list.RemoveAt((int)index);
            }
        }

        public void Clear(List<object> list)
        {
            list?.Clear();
        }

        public double Count(List<object> list) => list?.Count ?? 0;

        public object Get(List<object> list, double index)
        {
            if (list == null || index < 0 || index >= list.Count)
            {
                return null;
            }
            return list[(int)index];
        }

        public void Set(List<object> list, double index, object value)
        {
            if (list != null && index >= 0 && index < list.Count)
            {
                list[(int)index] = value;
            }
        }

        public bool Contains(List<object> list, object item)
        {
            return list?.Contains(item) ?? false;
        }

        public double IndexOf(List<object> list, object item)
        {
            return list?.IndexOf(item) ?? -1;
        }
    }

    /// <summary>
    /// 示例：创建一个可以暴露给 Lox 的文件操作类
    /// </summary>
    public class LoxFile
    {
        public string ReadAllText(string path)
        {
            try
            {
                return System.IO.File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

        public void WriteAllText(string path, string content)
        {
            try
            {
                System.IO.File.WriteAllText(path, content);
            }
            catch { }
        }

        public bool Exists(string path) => System.IO.File.Exists(path);

        public void Delete(string path)
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch { }
        }

        public double GetLength(string path)
        {
            try
            {
                var info = new System.IO.FileInfo(path);
                return info.Length;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// 示例：创建一个可以暴露给 Lox 的随机数生成器类
    /// </summary>
    public class LoxRandom
    {
        private readonly Random _random = new Random();

        public double Next() => _random.NextDouble();

        public double NextInt(double min, double max)
        {
            int minVal = (int)min;
            int maxVal = (int)max;
            return _random.Next(minVal, maxVal);
        }

        public double Range(double min, double max)
        {
            return min + (_random.NextDouble() * (max - min));
        }
    }
}
