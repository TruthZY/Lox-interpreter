using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lox
{
    /// <summary>
    /// 包装 .NET 对象，使其可以在 Lox 脚本中访问
    /// </summary>
    class LoxNativeObject : LoxInstance
    {
        private readonly object _nativeObject;
        private readonly Type _nativeType;
        private readonly Dictionary<string, PropertyInfo> _properties;
        private readonly Dictionary<string, MethodInfo> _methods;
        private readonly Dictionary<string, FieldInfo> _fields;

        public object NativeObject => _nativeObject;

        public LoxNativeObject(object nativeObject) : base(null)
        {
            _nativeObject = nativeObject ?? throw new ArgumentNullException(nameof(nativeObject));
            _nativeType = nativeObject.GetType();
            _properties = new Dictionary<string, PropertyInfo>();
            _methods = new Dictionary<string, MethodInfo>();
            _fields = new Dictionary<string, FieldInfo>();

            ScanType();
        }

        /// <summary>
        /// 扫描类型，收集可访问的属性和方法
        /// </summary>
        private void ScanType()
        {
            // 收集公共属性
            foreach (var prop in _nativeType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length == 0) // 排除索引器
                {
                    _properties[prop.Name.ToLower()] = prop;
                }
            }

            // 收集公共字段
            foreach (var field in _nativeType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                _fields[field.Name.ToLower()] = field;
            }

            // 收集公共方法（不包括属性和 Object 的方法）
            foreach (var method in _nativeType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!method.IsSpecialName && // 排除属性访问器
                    method.DeclaringType != typeof(object) && // 排除 Object 的方法
                    !_methods.ContainsKey(method.Name.ToLower()))
                {
                    _methods[method.Name.ToLower()] = method;
                }
            }
        }

        /// <summary>
        /// 获取属性或字段的值
        /// </summary>
        public new object Get(Token name)
        {
            string key = name.lexeme.ToLower();

            // 优先检查属性
            if (_properties.TryGetValue(key, out var property))
            {
                try
                {
                    var value = property.GetValue(_nativeObject);
                    return ConvertToLoxValue(value);
                }
                catch (Exception ex)
                {
                    throw new RuntimeError(name, $"Error getting property '{name.lexeme}': {ex.Message}");
                }
            }

            // 检查字段
            if (_fields.TryGetValue(key, out var field))
            {
                try
                {
                    var value = field.GetValue(_nativeObject);
                    return ConvertToLoxValue(value);
                }
                catch (Exception ex)
                {
                    throw new RuntimeError(name, $"Error getting field '{name.lexeme}': {ex.Message}");
                }
            }

            // 检查是否是方法（返回可调用对象）
            if (_methods.TryGetValue(key, out var method))
            {
                return new NativeMethod(_nativeObject, method, name.lexeme);
            }

            throw new RuntimeError(name, $"Undefined property '{name.lexeme}' on type '{_nativeType.Name}'.");
        }

        /// <summary>
        /// 设置属性或字段的值
        /// </summary>
        public new void Set(Token name, object value)
        {
            string key = name.lexeme.ToLower();

            // 优先设置属性
            if (_properties.TryGetValue(key, out var property))
            {
                if (!property.CanWrite)
                {
                    throw new RuntimeError(name, $"Property '{name.lexeme}' is read-only.");
                }

                try
                {
                    var convertedValue = ConvertToNativeValue(value, property.PropertyType);
                    property.SetValue(_nativeObject, convertedValue);
                    return;
                }
                catch (Exception ex)
                {
                    throw new RuntimeError(name, $"Error setting property '{name.lexeme}': {ex.Message}");
                }
            }

            // 设置字段
            if (_fields.TryGetValue(key, out var field))
            {
                try
                {
                    var convertedValue = ConvertToNativeValue(value, field.FieldType);
                    field.SetValue(_nativeObject, convertedValue);
                    return;
                }
                catch (Exception ex)
                {
                    throw new RuntimeError(name, $"Error setting field '{name.lexeme}': {ex.Message}");
                }
            }

            throw new RuntimeError(name, $"Undefined property '{name.lexeme}' on type '{_nativeType.Name}'.");
        }

        /// <summary>
        /// 将 .NET 值转换为 Lox 值
        /// </summary>
        private object ConvertToLoxValue(object value)
        {
            if (value == null) return null;

            // 基本类型直接返回
            if (value is double || value is string || value is bool) return value;

            // 数值类型转换为 double
            if (value is int || value is long || value is float || value is decimal)
            {
                return Convert.ToDouble(value);
            }

            // 包装 .NET 对象
            return new LoxNativeObject(value);
        }

        /// <summary>
        /// 将 Lox 值转换为 .NET 值
        /// </summary>
        private object ConvertToNativeValue(object value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    throw new InvalidCastException($"Cannot assign null to non-nullable type '{targetType.Name}'.");
                }
                return null;
            }

            // 解包 LoxNativeObject
            if (value is LoxNativeObject nativeObj)
            {
                value = nativeObj.NativeObject;
            }

            // 类型已经匹配
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            // 数值转换
            if (value is double d)
            {
                if (targetType == typeof(int)) return (int)d;
                if (targetType == typeof(long)) return (long)d;
                if (targetType == typeof(float)) return (float)d;
                if (targetType == typeof(decimal)) return (decimal)d;
            }

            // 字符串转换
            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            // 使用 Convert 类
            return Convert.ChangeType(value, targetType);
        }

        public override string ToString()
        {
            return $"<native {_nativeType.Name}>";
        }
    }

    /// <summary>
    /// 包装 .NET 方法，使其可以作为 Lox 函数调用
    /// </summary>
    class NativeMethod : ICallable
    {
        private readonly object _target;
        private readonly MethodInfo _method;
        private readonly string _name;

        public int Arity => _method.GetParameters().Length;

        public NativeMethod(object target, MethodInfo method, string name)
        {
            _target = target;
            _method = method;
            _name = name;
        }

        public object Call(Interpreter interpreter, List<object> arguments)
        {
            var parameters = _method.GetParameters();

            if (arguments.Count != parameters.Length)
            {
                throw new RuntimeError(
                    new Token(TokenType.IDENTIFIER, _name, null, 0),
                    $"Expected {parameters.Length} arguments but got {arguments.Count}.");
            }

            // 转换参数
            var convertedArgs = new object[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
            {
                convertedArgs[i] = ConvertArgument(arguments[i], parameters[i].ParameterType);
            }

            try
            {
                var result = _method.Invoke(_target, convertedArgs);
                return ConvertResult(result);
            }
            catch (TargetInvocationException ex)
            {
                throw new RuntimeError(
                    new Token(TokenType.IDENTIFIER, _name, null, 0),
                    $"Method '{_name}' threw an exception: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private object ConvertArgument(object value, Type targetType)
        {
            if (value == null) return null;

            // 解包 LoxNativeObject
            if (value is LoxNativeObject nativeObj)
            {
                value = nativeObj.NativeObject;
            }

            // 类型已经匹配
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            // 数值转换
            if (value is double d)
            {
                if (targetType == typeof(int)) return (int)d;
                if (targetType == typeof(long)) return (long)d;
                if (targetType == typeof(float)) return (float)d;
                if (targetType == typeof(decimal)) return (decimal)d;
            }

            // 字符串转换
            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            return Convert.ChangeType(value, targetType);
        }

        private object ConvertResult(object value)
        {
            if (value == null) return null;

            // 基本类型直接返回
            if (value is double || value is string || value is bool) return value;

            // 数值类型转换为 double
            if (value is int || value is long || value is float || value is decimal)
            {
                return Convert.ToDouble(value);
            }

            // 包装 .NET 对象
            return new LoxNativeObject(value);
        }

        public override string ToString()
        {
            return $"<native method {_name}>";
        }
    }
}
