/*
Copyright(c) 2009, Stefan Simek
Copyright(c) 2015, Vladyslav Taranov

MIT License

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
#if FEAT_IKVM
using IKVM.Reflection;
using IKVM.Reflection.Emit;
using Type = IKVM.Reflection.Type;
using MissingMethodException = System.MissingMethodException;
using MissingMemberException = System.MissingMemberException;
using DefaultMemberAttribute = System.Reflection.DefaultMemberAttribute;
using Attribute = IKVM.Reflection.CustomAttributeData;
using BindingFlags = IKVM.Reflection.BindingFlags;
#else
using System.Reflection;
using System.Reflection.Emit;
#endif

namespace TriAxis.RunSharp
{
    using Language.RunSharp.Properties;

    public struct AttributeType
	{
	    readonly Type _t;

		private AttributeType(Type t)
		{
			_t = t;
		}

		public static implicit operator Type(AttributeType t)
		{
			return t._t;
		}

		public static implicit operator AttributeType(Type t)
		{
		    if (!Helpers.IsAttribute(t))
		        throw new ArgumentException("Attribute types must derive from the 'Attribute' class", nameof(t));

			return new AttributeType(t);
		}

		public static implicit operator AttributeType(TypeGen tg)
		{
            if (!Helpers.IsAttribute(tg.TypeBuilder))
                throw new ArgumentException("Attribute types must derive from the 'Attribute' class", "t");

			return new AttributeType(tg.TypeBuilder);
		}
	}

	public class AttributeGen
	{
	    readonly Type _attributeType;
	    readonly object[] _args;
	    readonly ApplicableFunction _ctor;
		Dictionary<PropertyInfo, object> _namedProperties;
		Dictionary<FieldInfo, object> _namedFields;
	    readonly ITypeMapper _typeMapper;
	    public ITypeMapper TypeMapper => _typeMapper;

	    internal AttributeGen(AttributeTargets target, AttributeType attributeType, object[] args, ITypeMapper typeMapper)
		{
            _typeMapper = typeMapper;
            if (args != null)
			{
				foreach (object arg in args)
				{
					CheckValue(arg);
				}
			}

			// TODO: target validation

			_attributeType = attributeType;
		    
		    Operand[] argOperands;
			if (args == null || args.Length == 0)
			{
				_args = EmptyArray<object>.Instance;
				argOperands = Operand.EmptyArray;
			}
			else
			{
				_args = args;
				argOperands = new Operand[args.Length];
				for (int i = 0; i < args.Length; i++)
				{
					argOperands[i] = GetOperand(args[i]);
				}
			}

			_ctor = _typeMapper.TypeInfo.FindConstructor(attributeType, argOperands);
		}
        
        static bool IsValidAttributeParamType(Type t)
		{
		    return t != null && (t.IsPrimitive || t.IsEnum || Helpers.IsAssignableFrom(typeof(Type), t) || t.FullName == typeof(string).FullName);
		}

		static bool IsSingleDimensionalZeroBasedArray(Array a)
		{
			return a != null && a.Rank == 1 && a.GetLowerBound(0) == 0;
		}

		void CheckValue(object arg)
		{
			if (arg == null)
				throw new ArgumentNullException();
			Type t = _typeMapper.MapType(arg.GetType());

			if (IsValidAttributeParamType(t))
				return;
			if (IsSingleDimensionalZeroBasedArray(arg as Array) && IsValidAttributeParamType(t.GetElementType()))
				return;

			throw new ArgumentException();
		}

		static Operand GetOperand(object arg)
		{
			return Operand.FromObject(arg);
		}

		public AttributeGen SetField(string name, object value)
		{
			CheckValue(value);

			FieldInfo fi = (FieldInfo)_typeMapper.TypeInfo.FindField(_attributeType, name, false).Member;

			SetFieldIntl(fi, value);
			return this;
		}

		void SetFieldIntl(FieldInfo fi, object value)
		{
			if (_namedFields != null)
			{
				if (_namedFields.ContainsKey(fi))
					throw new InvalidOperationException(string.Format(Messages.ErrAttributeMultiField, fi.Name));
			}
			else
			{
				_namedFields = new Dictionary<FieldInfo, object>();
			}

			_namedFields[fi] = value;
		}

		public AttributeGen SetProperty(string name, object value)
		{
			CheckValue(value);

			PropertyInfo pi = (PropertyInfo)_typeMapper.TypeInfo.FindProperty(_attributeType, name, null, false).Method.Member;

			SetPropertyIntl(pi, value);
			return this;
		}

		void SetPropertyIntl(PropertyInfo pi, object value)
		{
			if (!pi.CanWrite)
				throw new InvalidOperationException(string.Format(Messages.ErrAttributeReadOnlyProperty, pi.Name));

			if (_namedProperties != null)
			{
				if (_namedProperties.ContainsKey(pi))
					throw new InvalidOperationException(string.Format(Messages.ErrAttributeMultiProperty, pi.Name));
			}
			else
			{
				_namedProperties = new Dictionary<PropertyInfo, object>();
			}

			_namedProperties[pi] = value;
		}

		public AttributeGen Set(string name, object value)
		{
			CheckValue(value);

			for (Type t = _attributeType; t != null; t = t.BaseType)
			{
				foreach (IMemberInfo mi in _typeMapper.TypeInfo.GetFields(t))
				{
					if (mi.Name == name && !mi.IsStatic)
					{
						SetFieldIntl((FieldInfo)mi.Member, value);
						return this;
					}
				}

				ApplicableFunction af = OverloadResolver.Resolve(_typeMapper.TypeInfo.Filter(_typeMapper.TypeInfo.GetProperties(t), name, false, false, false), _typeMapper, Operand.EmptyArray);
				if (af != null)
				{
					SetPropertyIntl((PropertyInfo)af.Method.Member, value);
					return this;
				}
			}

			throw new MissingMemberException(Messages.ErrMissingProperty);
		}

		CustomAttributeBuilder GetAttributeBuilder()
		{
			ConstructorInfo ci = (ConstructorInfo)_ctor.Method.Member;

			if (_namedProperties == null && _namedFields == null)
			{
				return new CustomAttributeBuilder(ci, _args);
			}

			if (_namedProperties == null)
			{
				return new CustomAttributeBuilder(ci, _args, ArrayUtils.ToArray(_namedFields.Keys), ArrayUtils.ToArray(_namedFields.Values));
			}

			if (_namedFields == null)
			{
				return new CustomAttributeBuilder(ci, _args, ArrayUtils.ToArray(_namedProperties.Keys), ArrayUtils.ToArray(_namedProperties.Values));
			}

			return new CustomAttributeBuilder(ci, _args,
				ArrayUtils.ToArray(_namedProperties.Keys), ArrayUtils.ToArray(_namedProperties.Values),
				ArrayUtils.ToArray(_namedFields.Keys), ArrayUtils.ToArray(_namedFields.Values));
		}

		internal static void ApplyList(ref List<AttributeGen> customAttributes, Action<CustomAttributeBuilder> setCustomAttribute)
		{
			if (customAttributes != null)
			{
				foreach (AttributeGen ag in customAttributes)
					setCustomAttribute(ag.GetAttributeBuilder());

				customAttributes = null;
			}
		}
	}

	public class AttributeGen<TOuterContext> : AttributeGen
	{
	    readonly TOuterContext _context;

		internal AttributeGen(TOuterContext context, AttributeTargets target, AttributeType attributeType, object[] args, ITypeMapper typeMapper)
			: base(target, attributeType, args, typeMapper)
		{
			_context = context;
		}

		internal static AttributeGen<TOuterContext> CreateAndAdd(TOuterContext context, ref List<AttributeGen> list, AttributeTargets target, AttributeType attributeType, object[] args, ITypeMapper typeMapper)
		{
			AttributeGen<TOuterContext> ag = new AttributeGen<TOuterContext>(context, target, attributeType, args, typeMapper);
			if (list == null)
				list = new List<AttributeGen>();
			list.Add(ag);
			return ag;
		}

		public new AttributeGen<TOuterContext> SetField(string name, object value)
		{
			base.SetField(name, value);
			return this;
		}

		public new AttributeGen<TOuterContext> SetProperty(string name, object value)
		{
			base.SetProperty(name, value);
			return this;
		}

		public new AttributeGen<TOuterContext> Set(string name, object value)
		{
			base.Set(name, value);
			return this;
		}

		public TOuterContext End()
		{
			return _context;
		}
	}
}
