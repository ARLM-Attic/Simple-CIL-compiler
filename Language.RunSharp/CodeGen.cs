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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TryAxis.RunSharp;
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

    using Operands;

    public interface ICodeGenBasicContext
    {
        ITypeMapper TypeMapper { get; }
        StaticFactory StaticFactory { get; }
        ExpressionFactory ExpressionFactory { get; }
    }

    interface ICodeGenContext : IMemberInfo, ISignatureGen, IDelayedDefinition, IDelayedCompletion, ICodeGenBasicContext
    {
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Typical implementation invokes XxxBuilder.GetILGenerator() which is a method as well.")]
		ILGenerator GetILGenerator();

		Type OwnerType { get; }
		bool SupportsScopes { get; }
    }

	public partial class CodeGen
	{
	    readonly ConstructorGen _cg;
		bool _chainCalled;
		bool _reachable = true;
		bool _hasRetVar, _hasRetLabel;
		LocalBuilder _retVar;
		Label _retLabel;
	    readonly Stack<Block> _blocks = new Stack<Block>();
	    readonly Dictionary<string, Label> _labels = new Dictionary<string, Label>();
	    readonly Dictionary<string, Operand> _namedLocals = new Dictionary<string, Operand>();

		internal ILGenerator IL { get; }
	    internal ICodeGenContext Context { get; }

	    internal CodeGen(ICodeGenContext context)
		{
			Context = context;
			_cg = context as ConstructorGen;

			if (_cg != null && _cg.IsStatic)
				// #14 - cg is relevant for instance constructors - it wreaks havoc in a static constructor
				_cg = null;

			IL = context.GetILGenerator();
		}

		/*public static CodeGen CreateDynamicMethod(string name, Type returnType, params Type[] parameterTypes, Type owner, bool skipVisibility)
		{
			DynamicMethod dm = new DynamicMethod(name, returnType, parameterTypes, owner, skipVisibility);
			return new CodeGen(method.GetILGenerator(), defaultType, method.ReturnType, method.IsStatic, parameterTypes);
		}

		public static CodeGen FromMethodBuilder(MethodBuilder builder, params Type[] parameterTypes)
		{
			return new CodeGen(builder.GetILGenerator(), builder.DeclaringType, builder.ReturnType, builder.IsStatic, parameterTypes);
		}

		public static CodeGen FromConstructorBuilder(ConstructorBuilder builder, params Type[] parameterTypes)
		{
			return new CodeGen(builder.GetILGenerator(), builder.DeclaringType, builder.ReturnType, builder.IsStatic, parameterTypes);
		}*/

		#region Arguments
		public ContextualOperand This()
		{
			if (Context.IsStatic)
				throw new InvalidOperationException(Messages.ErrCodeStaticThis);

		    Type ownerType = Context.OwnerType;
            
		    if (Context.OwnerType.IsValueType)
		    {
		        var m = Context.Member as MethodInfo;
		        if (m != null && m.IsVirtual)
		            ownerType = ownerType.MakeByRefType();
		    }

		    Operand arg = new _Arg(0, ownerType);
            return new ContextualOperand(arg, TypeMapper);
		}

		public ContextualOperand Base()
		{
			if (Context.IsStatic)
				return new ContextualOperand(new StaticTarget(Context.OwnerType.BaseType), TypeMapper);
			else
				return new ContextualOperand(new _Base(Context.OwnerType.BaseType), TypeMapper);
		}

		int ThisOffset => Context.IsStatic ? 0 : 1;

	    public ContextualOperand PropertyValue()
		{
			Type[] parameterTypes = Context.ParameterTypes;
			return new ContextualOperand(new _Arg(ThisOffset + parameterTypes.Length - 1, parameterTypes[parameterTypes.Length - 1]), TypeMapper);
		}

		public ContextualOperand Arg(string name)
		{
			ParameterGen param = Context.GetParameterByName(name);
			return new ContextualOperand(new _Arg(ThisOffset + param.Position - 1, param.Type), TypeMapper);
		}
		#endregion

		#region Locals
		public ContextualOperand Local()
		{
			return new ContextualOperand(new _Local(this), TypeMapper);
		}

		public ContextualOperand Local(Operand init)
		{
			Operand var = Local();
			Assign(var, init);
			return new ContextualOperand(var, TypeMapper);
		}

#if FEAT_IKVM

        public ContextualOperand Local(System.Type type)
	    {
	        return Local(TypeMapper.MapType(type));
	    }
        
#endif

	    public ContextualOperand Local(Type type)
		{
			return new ContextualOperand(new _Local(this, type), TypeMapper);
		}

#if FEAT_IKVM

        public ContextualOperand Local(System.Type type, Operand init)
	    {
	        return Local(TypeMapper.MapType(type), init);
	    }
        
#endif

	    public ContextualOperand Local(Type type, Operand init)
		{
			Operand var = Local(type);
			Assign(var, init);
			return new ContextualOperand(var, TypeMapper);
		}
		#endregion

		bool HasReturnValue
		{
			get
			{
				Type returnType = Context.ReturnType;
			    return returnType != null && !Helpers.AreTypesEqual(returnType, typeof(void), TypeMapper);
			}
		}

		void EnsureReturnVariable()
		{
			if (_hasRetVar)
				return;

			_retLabel = IL.DefineLabel();
			if (HasReturnValue)
				_retVar = IL.DeclareLocal(Context.ReturnType);
			_hasRetVar = true;
		}

		public bool IsCompleted => _blocks.Count == 0 && !_reachable && _hasRetVar == _hasRetLabel;

	    internal void Complete()
		{
			if (_blocks.Count > 0)
				throw new InvalidOperationException(Messages.ErrOpenBlocksRemaining);

			if (_reachable)
			{
				if (HasReturnValue)
					throw new InvalidOperationException(string.Format(null, Messages.ErrMethodMustReturnValue, Context));
				else
					Return();
			}

			if (_hasRetVar && !_hasRetLabel)
			{
				IL.MarkLabel(_retLabel);
				if (_retVar != null)
					IL.Emit(OpCodes.Ldloc, _retVar);
				IL.Emit(OpCodes.Ret);
				_hasRetLabel = true;
			}
		}

		class _Base : _Arg
		{
			public _Base(Type type) : base(0, type) { }

			internal override bool SuppressVirtual => true;
		}

		class _Arg : Operand
		{
		    readonly ushort _index;
		    readonly Type _type;

			public _Arg(int index, Type type)
			{
				_index = checked((ushort)index);
				_type = type;
			}

			internal override void EmitGet(CodeGen g)
			{
				g.EmitLdargHelper(_index);

				if (IsReference)
					g.EmitLdindHelper(GetReturnType(g.TypeMapper));
			}

			internal override void EmitSet(CodeGen g, Operand value, bool allowExplicitConversion)
			{
				if (IsReference)
				{
					g.EmitLdargHelper(_index);
					g.EmitStindHelper(GetReturnType(g.TypeMapper), value, allowExplicitConversion);
				}
				else
				{
					g.EmitGetHelper(value, GetReturnType(g.TypeMapper), allowExplicitConversion);
					g.EmitStargHelper(_index);
				}
			}

			internal override void EmitAddressOf(CodeGen g)
			{
				if (IsReference)
				{
					g.EmitLdargHelper(_index);
				}
				else
				{
					if (_index <= byte.MaxValue)
						g.IL.Emit(OpCodes.Ldarga_S, (byte)_index);
					else
						g.IL.Emit(OpCodes.Ldarga, _index);
				}
			}

			bool IsReference => _type.IsByRef;

		    public override Type GetReturnType(ITypeMapper typeMapper) => IsReference ? _type.GetElementType() : _type;

		    internal override bool TrivialAccess => true;
		}

		internal class _Local : Operand
		{
		    readonly CodeGen _owner;
			LocalBuilder _var;
		    readonly Block _scope;
			Type _t, _tHint;

			public _Local(CodeGen owner)
			{
				_owner = owner;
				_scope = owner.GetBlockForVariable();
			}
			public _Local(CodeGen owner, Type t)
			{
				_owner = owner; _t = t;
				_scope = owner.GetBlockForVariable();
			}

			public _Local(CodeGen owner, LocalBuilder var)
			{
				_owner = owner;
				_var = var;
				_t = var.LocalType;
			}

			void CheckScope(CodeGen g)
			{
				if (g != _owner)
					throw new InvalidOperationException(Messages.ErrInvalidVariableContext);
                if (_scope != null && !_owner._blocks.Contains(_scope))
                    throw new InvalidOperationException(Messages.ErrInvalidVariableScope);
            }

			internal override void EmitGet(CodeGen g)
			{
				CheckScope(g);

				if (_var == null)
					throw new InvalidOperationException(Messages.ErrUninitializedVarAccess);

				g.IL.Emit(OpCodes.Ldloc, _var);
			}

			internal override void EmitSet(CodeGen g, Operand value, bool allowExplicitConversion)
			{
				CheckScope(g);

				if (_t == null)
					_t = value.GetReturnType(g.TypeMapper);

				if (_var == null)
					_var = g.IL.DeclareLocal(_t);

				g.EmitGetHelper(value, _t, allowExplicitConversion);
				g.IL.Emit(OpCodes.Stloc, _var);
			}

			internal override void EmitAddressOf(CodeGen g)
			{
				CheckScope(g);

				if (_var == null)
				{
					RequireType();
					_var = g.IL.DeclareLocal(_t);
				}

				g.IL.Emit(OpCodes.Ldloca, _var);
			}

		    public override Type GetReturnType(ITypeMapper typeMapper)
		    {
		        RequireType();
		        return _t;
		    }

		    void RequireType()
			{
				if (_t == null)
				{
					if (_tHint != null)
						_t = _tHint;
					else
						throw new InvalidOperationException(Messages.ErrUntypedVarAccess);
				}
			}

			internal override bool TrivialAccess => true;

		    internal override void AssignmentHint(Operand op)
			{
				if (_tHint == null)
					_tHint = GetType(op, _owner.TypeMapper);
			}
		}

		class StaticTarget : Operand
		{
		    public StaticTarget(Type t) { _type = t; }

		    readonly Type _type;

		    public override Type GetReturnType(ITypeMapper typeMapper)
		    {
		        return _type;
		    }

		    internal override bool IsStaticTarget => true;
		}

		public Operand this[string localName] // Named locals support. 
		{
			get
			{
				Operand target;
				if (!_namedLocals.TryGetValue(localName, out target))
					throw new InvalidOperationException(Messages.ErrUninitializedVarAccess);
				return target;
			}
			set
			{
				Operand target;
				if (_namedLocals.TryGetValue(localName, out target))
					// run in statement form; C# left-to-right evaluation semantics "just work"
					Assign(target, value);
				else
					_namedLocals.Add(localName, Local(value));
			}
		}

		public void Label(string labelName)
		{
			Label label;
			if (!_labels.TryGetValue(labelName, out label))
				_labels.Add(labelName, label = IL.DefineLabel());
			IL.MarkLabel(label);
		}

		public void Goto(string labelName)
		{
			Label label;
			if (!_labels.TryGetValue(labelName, out label))
				_labels.Add(labelName, label = IL.DefineLabel());
			IL.Emit(OpCodes.Br, label);
		}
	}
}
