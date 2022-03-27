using System.Reflection;
using System.Text.Json;

namespace WebApiPatch.Extensions
{

	public class PatchConfig
	{
		public Type[] DenyTypes { get; set; } = new Type[0];
		public Type[] IgnoreTypes { get; set; } = new Type[0];
		public string[] IgnoreProperties { get; set; } = new string[0];
	}

	public static class JsonElementExtensions
	{
		private static string FirstCharToUpper(string input) =>
			input switch
			{
				null => throw new ArgumentNullException(nameof(input)),
				"" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
				_ => input[0].ToString().ToUpper() + input.Substring(1)
			};

		private static string GetPropertyTypeName(Type propertyType)
		{
			return propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ?
				 $"{propertyType.GetGenericArguments()[0].Name}?" :
				(propertyType.BaseType == typeof(System.Enum) ? "Enum" : propertyType.Name);
		}

		/// <summary>
		/// Processing the items of an array by creating a new instance if the array, adding all items from the Json array and 
		/// setting the new array as value for the property of the patch object
		/// </summary>
		/// <param name="element"></param>
		/// <param name="parent"></param>
		/// <param name="propertyInfo"></param>
		/// <param name="patchConfig"></param>
		private static void ProcessSingleArray(JsonElement element, object parent, PropertyInfo propertyInfo, PatchConfig patchConfig)
		{
			if (propertyInfo == null) throw new ArgumentNullException(nameof(propertyInfo));

			var targetElementType = propertyInfo.PropertyType.GetElementType();
			if (targetElementType == null)
				throw new ArgumentException(nameof(propertyInfo), $"{propertyInfo.Name} is not an array type");

			var arr = Array.CreateInstance(targetElementType, element.GetArrayLength());
			for (var i = 0; i < element.GetArrayLength(); i++)
			{
				var elementIndex = element[i];
				switch (GetPropertyTypeName(targetElementType))
				{
					case "Boolean":
					case "Boolean?":
						arr.SetValue(elementIndex.GetBoolean(), i);
						break;
					case "Byte":
					case "Byte?":
						arr.SetValue(elementIndex.GetByte(), i);
						break;
					case "DateTime":
					case "DateTime?":
						arr.SetValue(elementIndex.GetDateTime(), i);
						break;
					case "Decimal":
					case "Decimal?":
						arr.SetValue(elementIndex.GetDecimal(), i);
						break;
					case "Guid":
					case "Guid?":
						arr.SetValue(elementIndex.GetGuid(), i);
						break;
					case "Int16":
					case "Int16?":
						arr.SetValue(elementIndex.GetInt16(), i);
						break;
					case "Int32":
					case "Int32?":
						arr.SetValue(elementIndex.GetInt32(), i);
						break;
					case "Int64":
					case "Int64?":
						arr.SetValue(elementIndex.GetInt64(), i);
						break;
					case "Single":
					case "Single?":
						arr.SetValue(elementIndex.GetSingle(), i);
						break;
					case "Double":
					case "Double?":
						arr.SetValue(elementIndex.GetDouble(), i);
						break;
					case "String":
					case "String?":
						var elementStringValue = elementIndex.GetString();
						arr.SetValue(elementStringValue != string.Empty ? elementStringValue : null, i);
						break;
					default:
						var propertyStrValue = elementIndex.GetRawText();
						if (GetTypeAsEnum(propertyInfo.PropertyType, out _, out _))
						{
							// Target type is an enum. Parse json string to the enum value, throw exception if invalid.
							var enumValue = ConvertStringToEnumValue(propertyStrValue, propertyInfo.PropertyType);
							arr.SetValue(enumValue, i);
						}
						else if (elementIndex.ValueKind == JsonValueKind.Object)
						{
							// The json element indicates an object of some type, so create the intended target type,
							// then parse each json property with crossed fingers.
							var itemObj = Activator.CreateInstance(targetElementType);
							ProcessObject(elementIndex, targetElementType, itemObj, patchConfig);
							arr.SetValue(itemObj, i);
						}
						else
						{
							arr.SetValue(Convert.ChangeType(elementIndex.GetString(), propertyInfo.PropertyType), i);
						}
						break;
				}
			}
			propertyInfo.SetValue(parent, arr);
		}


		/// <summary>
		/// Processing the items of a collecion by clearing the target collection completely and adding all items from the Json collection
		/// </summary>
		/// <param name="element">Json element with collection</param>
		/// <param name="parent">Parent object instance</param>
		/// <param name="propertyInfo">Collection property of object to be patched</param>
		/// <param name="patchConfig">Config settings</param>
		private static void ProcessCollection(JsonElement element, object parent, PropertyInfo propertyInfo, PatchConfig patchConfig)
		{
			var listItemType = propertyInfo.PropertyType.GetGenericArguments()[0];
			var genListType = typeof(List<>);
			Type[] typeArgs = { listItemType };
			var constructedListType = genListType.MakeGenericType(typeArgs);
			var objArray = propertyInfo.GetValue(parent) ?? Activator.CreateInstance(constructedListType);
			MethodInfo addMethod = constructedListType.GetMethod("Add") ?? throw new ArgumentNullException(nameof(addMethod));
			MethodInfo clearMethod = constructedListType.GetMethod("Clear") ?? throw new ArgumentNullException(nameof(clearMethod));

			var targetList = Convert.ChangeType(objArray, constructedListType);
			clearMethod.Invoke(targetList, null);

			foreach (var item in element.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.Object)
				{
					var itemObj = Activator.CreateInstance(listItemType);
					ProcessObject(item, listItemType, itemObj, patchConfig);
					addMethod.Invoke(targetList, new[] { itemObj });
				}
			}
		}

		/// <summary>
		/// Processing the property arrays by setting the values from Json array to property array
		/// Only arrays or ICollecion<T> are supported
		/// </summary>
		/// <param name="element">Json element</param>
		/// <param name="parent">Parent Object</param>
		/// <param name="propertyInfo">Object Property to be set with Json array</param>
		/// <param name="arrayName">Name of the source array fro Json document</param>
		/// <param name="patchConfig">Config settings</param>
		private static void ProcessArray(JsonElement element, object parent, PropertyInfo propertyInfo, string arrayName, PatchConfig patchConfig)
		{
			if (propertyInfo.PropertyType.IsArray)
			{
				ProcessSingleArray(element, parent, propertyInfo, patchConfig);
				return;
			}

			if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
			{
				ProcessCollection(element, parent, propertyInfo, patchConfig);
				return;
			}

			throw new Exception($"Array type '{arrayName}' is not supported");
		}


		/// <summary>
		/// Processing the property by setting the value from Json property to object property
		/// </summary>
		/// <param name="jsonProperty">Json Property with Pathc data</param>
		/// <param name="propertyInfo">Object Property to be set with Json data</param>
		/// <param name="obj">Object instance to be patched</param>
		/// <param name="patchConfig">Config settings</param>
		private static void ProcessProperty(JsonProperty jsonProperty, PropertyInfo propertyInfo, object obj, PatchConfig patchConfig)
		{
			if (propertyInfo == null)
			{
				throw new Exception($"Property '{jsonProperty.Name}' not found");
			}

			// Filter for internal properties, which must not be patched
			if (patchConfig.IgnoreProperties.Any(c => string.Equals(propertyInfo.Name, c, StringComparison.InvariantCultureIgnoreCase)))
			{
				return;
			}

			if (jsonProperty.Value.ValueKind == JsonValueKind.Null)
			{
				if (propertyInfo.PropertyType.IsValueType && !(propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)))
				{
					throw new Exception($"Property '{jsonProperty.Name}' cannot be null");
				}
				propertyInfo.SetValue(obj, null);
			}
			else
			{
				switch (GetPropertyTypeName(propertyInfo.PropertyType))
				{
					case "Boolean":
					case "Boolean?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetBoolean());
						break;
					case "Byte":
					case "Byte?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetSingle());
						break;
					case "DateTime":
					case "DateTime?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetDateTime());
						break;
					case "Decimal":
					case "Decimal?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetDecimal());
						break;
					case "Guid":
					case "Guid?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetGuid());
						break;
					case "Int16":
					case "Int16?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetInt16());
						break;
					case "Int32":
					case "Int32?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetInt32());
						break;
					case "Int64":
					case "Int64?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetInt64());
						break;
					case "Single":
					case "Single?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetSingle());
						break;
					case "Double":
					case "Double?":
						propertyInfo.SetValue(obj, jsonProperty.Value.GetDouble());
						break;
					case "String":
					case "String?":
						propertyInfo.SetValue(obj, jsonProperty.Value.ToString() != string.Empty ? jsonProperty.Value.GetString() : null);
						break;
					case "Enum":
						if (Enum.TryParse(propertyInfo.PropertyType, jsonProperty.Value.GetString(), true, out object result))
						{
							propertyInfo.SetValue(obj, result);
						}
						break;
					default:
						var propertyStrValue = jsonProperty.Value.ToString();
						if (GetTypeAsEnum(propertyInfo.PropertyType, out _, out _))
						{
							// Target type is an enum. Parse json string to the enum value, throw exception if invalid.
							var enumValue = ConvertStringToEnumValue(propertyStrValue, propertyInfo.PropertyType);
							propertyInfo.SetValue(obj, enumValue);
						}
						else
						{
							// Property is a non-enum type, just try a runtime conversion
							propertyInfo.SetValue(obj, Convert.ChangeType(propertyStrValue, propertyInfo.PropertyType));
						}
						break;
				}
			}
		}


		/// <summary>
		/// Processing the object by going througn the object properties.
		/// </summary>
		/// <param name="data">Patch data</param>
		/// <param name="typeObject">Object type to be patched</param>
		/// <param name="obj">Object instance to be patched</param>
		/// <param name="patchConfig">Config settings</param>
		private static void ProcessObject(JsonElement data, Type typeObject, object obj, PatchConfig patchConfig)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));
			if (data.ValueKind != JsonValueKind.Object)
				throw new ArgumentException("Json element is not an object", nameof(data));

			foreach (var element in data.EnumerateObject())
			{
				var propertyName = FirstCharToUpper(element.Name);
				var propInfo = typeObject.GetProperty(propertyName);
				if (propInfo == null)
				{
					throw new Exception($"Failed to find property name {propertyName} in target type {typeObject.Name}");
				}

				if (element.Value.ValueKind == JsonValueKind.Object)
				{
					if (patchConfig.IgnoreTypes?.Any(c => c == propInfo.PropertyType) == true)
					{
						continue;
					}
					if (patchConfig.DenyTypes?.Any(c => c == propInfo.PropertyType) == true)
					{
						throw new Exception($"Patch of path '{element.Name}' is not allowed");
					}

					// If the target property is null then create an object using the default constructor
					var targetObject = propInfo.GetValue(obj);
					if (targetObject == null)
					{
						targetObject = Activator.CreateInstance(propInfo.PropertyType);
						propInfo.SetValue(obj, targetObject);
					}

					ProcessObject(element.Value, propInfo.PropertyType, targetObject, patchConfig);
					continue;
				}

				if (element.Value.ValueKind == JsonValueKind.Array)
				{
					ProcessArray(element.Value, obj, propInfo, element.Name, patchConfig);
					continue;
				}
				ProcessProperty(element, propInfo, obj, patchConfig);
			}
		}

		/// <summary>
		/// Determines if the type is an enum or nullable enum
		/// </summary>
		/// <param name="type">Type to evaluate</param>
		/// <param name="enumType">output: Underlying enum type</param>
		/// <param name="isNullable">output: is the enum type nullable</param>
		/// <returns>True if the type is an enum or nullable enum</returns>
		private static bool GetTypeAsEnum(Type type, out Type enumType, out bool isNullable)
		{
			if (type.IsEnum)
			{
				enumType = type;
				isNullable = false;
				return true;
			}

			var underlyingType = Nullable.GetUnderlyingType(type);
			if (underlyingType != null && underlyingType.IsEnum)
			{
				enumType = underlyingType;
				isNullable = true;
				return true;
			}

			enumType = null;
			isNullable = false;
			return false;
		}

		/// <summary>
		/// Parses a string as an enum type or nullable enum type
		/// </summary>
		/// <param name="strValue">Enum value represented as string</param>
		/// <param name="enumType">Target type</param>
		/// <returns>Converted enum type</returns>
		/// <exception cref="Exception"></exception>
		private static object? ConvertStringToEnumValue(string strValue, Type enumType)
		{
			if (!GetTypeAsEnum(enumType, out var underlyingEnumType, out var enumIsNullable))
				throw new Exception($"Type is not an enum type: {enumType.Name}");

			if (string.IsNullOrEmpty(strValue))
			{
				if (!enumIsNullable)
					throw new Exception($"Value cannot be null for enum type: {underlyingEnumType.Name}");
				return null;
			}

			if (!Enum.TryParse(underlyingEnumType, strValue, ignoreCase: true, out var parsedEnumValue))
				throw new Exception($"Invalid value for enum type: {underlyingEnumType.Name}. Value: {strValue}");

			return parsedEnumValue;
		}

		/// <summary>
		/// Patching JOSN data to given object by assigning the values from matching class properties from JsonElement to the object instance
		/// </summary>
		/// <param name="data">JsonElement with data to be patched into object</param>
		/// <param name="obj">Object instance to be patched</param>
		/// <param name="configurer">Patch configuration method, allowing to specify Types to be denied or Types and property names to be ignored</param>
		/// <param name="ignoreProperties">List of property names, which should be ignoed and not patched</param>
		/// <typeparam name="T"></typeparam>
		public static void Patch<T>(this JsonElement data, T obj, Action<PatchConfig>? configurer = null) where T : class
		{
			if (data.ValueKind != JsonValueKind.Object)
				throw new Exception("Only object types can be patched");

			if (obj == null)
				throw new Exception("Object must not be null");

			var patchConfig = new PatchConfig();
			if (configurer != null)
				configurer(patchConfig);

			// Build ignore list for internal properties
			var tmpList = new List<string> { "Id", "Number" };
			if (patchConfig.IgnoreProperties != null)
				tmpList.AddRange(patchConfig.IgnoreProperties);
			patchConfig.IgnoreProperties = tmpList.ToArray();
			ProcessObject(data, obj.GetType(), obj, patchConfig);
		}

		public static bool HasRelationalData(this JsonElement data, string propertyName)
		{
			var property = data.EnumerateObject().FirstOrDefault(c => string.Equals(c.Name, propertyName, StringComparison.InvariantCultureIgnoreCase));
			return property.Value.ValueKind == JsonValueKind.Array || property.Value.ValueKind == JsonValueKind.Object;
		}

	}
}