// Created by Steve McConnel Feb 2, 2012 by copying and editing IcuTranslitEncConverter.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Win32;                  // for RegistryKey

using ECInterfaces;                     // for IEncConverter

namespace SilEncConverters40
{
	/// <summary>
	/// Managed ICU Converter EncConverter
	/// </summary>
	public class IcuConvEncConverter : EncConverter
	{
		#region DLLImport Statements
		// On Linux looks for libIcuConvEC.so (adds lib- and -.so)
		[DllImport("IcuConvEC", EntryPoint="IcuConvEC_Initialize")]
		static extern unsafe int CppInitialize (
			[MarshalAs(UnmanagedType.LPStr)] string strConverterSpec);
		
		[DllImport("IcuConvEC", EntryPoint="IcuConvEC_PreConvert")]
		static extern unsafe int CppPreConvert(
			int eInEncodingForm, ref int eInFormEngine,
			int eOutEncodingForm, ref int eOutFormEngine,
			ref int eNormalizeOutput, bool bForward, int nInactivityWarningTimeOut);
		
		[DllImport("IcuConvEC", EntryPoint="IcuConvEC_DoConvert")]
		static extern unsafe int CppDoConvert(
			byte* lpInputBuffer, int nInBufLen,
			byte* lpOutputBuffer, int *npOutBufLen);

		[DllImport("IcuConvEC", EntryPoint="IcuConvEC_ConverterNameList_start")]
		static extern unsafe int CppConverterNameList_start();

		[DllImport("IcuConvEC", EntryPoint="IcuConvEC_ConverterNameList_next")]
		static extern unsafe string CppConverterNameList_next();

		[DllImport("IcuConvEC", EntryPoint="IcuConvEC_GetDisplayName")]
		static extern unsafe string CppGetDisplayName(string strID);
		#endregion DLLImport Statements

		#region Member Variable Definitions
		public const string strDisplayName = "ICU Converter";
		public const string strHtmlFilename = "ICU Converters Plug-in About box.htm";
		#endregion Member Variable Definitions

		#region Initialization
		/// <summary>
		/// The class constructor. </summary>
		public IcuConvEncConverter()
			: base (
				typeof(IcuConvEncConverter).FullName,
				EncConverters.strTypeSILicuConv)
		{
		}

		/// <summary>
		/// The class destructor. </summary>
		~IcuConvEncConverter()
		{
			Unload();
		}

		public override void Initialize(
			string converterName,
			string converterSpec,
			ref string lhsEncodingID,
			ref string rhsEncodingID,
			ref ConvType conversionType,
			ref Int32 processTypeFlags,
			Int32 codePageInput,
			Int32 codePageOutput,
			bool bAdding)
		{
			System.Diagnostics.Debug.WriteLine("IcuConv EC Initialize BEGIN");
			// let the base class have first stab at it
			base.Initialize(converterName, converterSpec, ref lhsEncodingID, ref rhsEncodingID,
				ref conversionType, ref processTypeFlags, codePageInput, codePageOutput,
				bAdding);

			// the only thing we want to add (now that the convType can be less than accurate)
			//  is to make sure it's unidirectional
			switch (conversionType)
			{
				case ConvType.Legacy_to_from_Legacy:
					conversionType = ConvType.Legacy_to_Legacy;
					break;
				case ConvType.Legacy_to_from_Unicode:
					conversionType = ConvType.Legacy_to_Unicode;
					break;
				case ConvType.Unicode_to_from_Legacy:
					conversionType = ConvType.Unicode_to_Legacy;
					break;
				case ConvType.Unicode_to_from_Unicode:
				    conversionType = ConvType.Unicode_to_Unicode;
				    break;
				default:
					break;
			}
			System.Diagnostics.Debug.WriteLine("IcuConv EC Initialize END");
		}
		#endregion Initialization

		#region Misc helpers
		protected bool IsFileLoaded()
		{
			return false;
		}

		protected void Unload()
		{
		}

		protected override EncodingForm DefaultUnicodeEncForm(bool bForward, bool bLHS)
		{
			// if it's unspecified, then we want UTF-16
			return EncodingForm.UTF16;
		}

		protected unsafe void Load(string strConvID)
		{
			System.Diagnostics.Debug.WriteLine("IcuConv Load BEGIN");
			System.Diagnostics.Debug.WriteLine("Calling CppInitialize");
			int status = 0;
			try
			{
				status = CppInitialize(strConvID);
			}
			catch (DllNotFoundException exc)
			{
				throw new Exception("Failed to load .so file. Check path.");
			}
			catch (EntryPointNotFoundException exc)
			{
				throw new Exception("Failed to find function in .so file.");
			}
			if (status != 0)
			{
				throw new Exception("CppInitialize failed.");
			}
			System.Diagnostics.Debug.WriteLine("IcuConv Load END");
		}
		#endregion Misc helpers

		#region Abstract Base Class Overrides
		protected override unsafe void PreConvert(
			EncodingForm       eInEncodingForm,
			ref EncodingForm   eInFormEngine,
			EncodingForm       eOutEncodingForm,
			ref EncodingForm   eOutFormEngine,
			ref NormalizeFlags eNormalizeOutput,
			bool               bForward)
		{
			// let the base class do it's thing first
			base.PreConvert(eInEncodingForm, ref eInFormEngine,
				eOutEncodingForm, ref eOutFormEngine,
				ref eNormalizeOutput, bForward);

			if (NormalizeLhsConversionType(ConversionType) == NormConversionType.eUnicode)
			{
				if (ECNormalizeData.IsUnix)
				{
					// returning this value will cause the input Unicode data (of any form,
					// UTF16, BE, etc.) to be converted to UTF8 narrow bytes before calling
					// DoConvert.
					eInFormEngine = EncodingForm.UTF8Bytes;
				}
				else
				{
					eInFormEngine = EncodingForm.UTF16;
				}
			}
			else
			{
				// legacy
				eInFormEngine = EncodingForm.LegacyBytes;
			}

			if (NormalizeRhsConversionType(ConversionType) == NormConversionType.eUnicode)
			{
				if (ECNormalizeData.IsUnix)
				{
					eOutFormEngine = EncodingForm.UTF8Bytes;
				}
				else
				{
					eOutFormEngine = EncodingForm.UTF16;
				}
			}
			else
			{
				eOutFormEngine = EncodingForm.LegacyBytes;
			}

			// do the load at this point.
			Load(ConverterIdentifier);
			
			// Finally, let the C++ code do its thing.
			int encInForm = (int)eInEncodingForm;
			int encInEngine = (int)eInFormEngine;
			int encOutForm = (int)eOutEncodingForm;
			int encOutEngine = (int)eOutFormEngine;
			int normOutput = (int)eNormalizeOutput;
			CppPreConvert(encInForm, ref encInEngine, encOutForm, ref encOutEngine,
				ref normOutput, bForward, 0);
			eInFormEngine = (EncodingForm)encInEngine;
			eOutFormEngine = (EncodingForm)encOutEngine;
			eNormalizeOutput = (NormalizeFlags)normOutput;
		}

		[CLSCompliant(false)]
		protected override unsafe void DoConvert(
			byte*   lpInBuffer,
			int     nInLen,
			byte*   lpOutBuffer,
			ref int rnOutLen)
		{
            System.Diagnostics.Debug.WriteLine("IcuConvEC.DoConvert BEGIN()");
			int status = 0;
			fixed(int* pnOut = &rnOutLen)
			{
				status = CppDoConvert(lpInBuffer, nInLen, lpOutBuffer, pnOut);
			}
			if (status != 0)
			{
				EncConverters.ThrowError(ErrStatus.Exception, "CppDoConvert() failed.");
			}
            System.Diagnostics.Debug.WriteLine("IcuConvEC.DoConvert END()");
		}

		protected override string GetConfigTypeName
		{
			get { return typeof(IcuConvEncConverterConfig).AssemblyQualifiedName; }
		}

		#endregion Abstract Base Class Overrides
		
		#region Additional public methods to access the C++ DLL.
		/// <summary>
		/// Gets the available ICU converter specifications.
		/// </summary>
		public static unsafe List<string> GetAvailableConverterSpecs()
		{
			int count = CppConverterNameList_start();
			List<string> specs = new List<string>(count);
			for (int i = 0; i < count; ++i)
				specs.Add( CppConverterNameList_next() );
			return specs;
		}
		
		/// <summary>
		/// Gets the display name of the given ICU converter specification.
		/// In practice, the output may be the same as the input.
		/// </summary>
		public static unsafe string GetDisplayName(string spec)
		{
			return CppGetDisplayName(spec);
		}
		#endregion
	}
}
