using System;
using System.Collections.Generic;
using Microsoft.Data.Edm;
using Microsoft.Data.Edm.Library;
using Newtonsoft.Json.Schema;

namespace JsonSchemaToEdmModel
{
    public class JsonSchemaConverter
    {
        public IEdmModel ToEdmModel(JSchema schema)
        {
            var model = new EdmModel();

            var container = new EdmEntityContainer("namespace", "containerName");

            ConvertToEdmEntityType(schema, container, null);


            model.AddElement(container);

            return model;
        }

        private EdmEntityType ConvertToEdmEntityType(JSchema schema, EdmEntityContainer container, string name)
        {
            var type = new EdmEntityType("root", name ?? "root");

            foreach (var property in schema.Properties)
            {
                if (property.Value.Type == null)
                    throw new Exception("Type specyfication missing.");

                var structuralType = MapPropertyToStructuralType(property, schema, container);

                if (structuralType != null)
                {
                    type.AddStructuralProperty(property.Key, structuralType);
                }
                else
                {
                    type.AddStructuralProperty(property.Key, ToEdmPrimitiveType(property.Value.Type.Value));
                }
            }

            container.AddEntitySet(name ?? "root", type);

            return type;
        }

        private IEdmTypeReference MapPropertyToStructuralType(KeyValuePair<string, JSchema> property, JSchema parent, EdmEntityContainer container)
        {
            switch (property.Value.Type)
            {
                case JSchemaType.String:
                    return MapStringProperties(property, parent);
                case JSchemaType.Object:
                    return MapObject(property, parent, container);
                case JSchemaType.None:
                case JSchemaType.Number:
                case JSchemaType.Integer:
                case JSchemaType.Boolean:
                case JSchemaType.Array:
                case JSchemaType.Null:
                case null:
                default:
                    return null;
            }
        }

        private IEdmTypeReference MapObject(KeyValuePair<string, JSchema> property, JSchema parent, EdmEntityContainer container)
        {
            var entityType = ConvertToEdmEntityType(property.Value, container, property.Key);

            bool isNullable = !parent.Required.Contains(property.Key);

            return new EdmEntityReferenceTypeReference(new EdmEntityReferenceType(entityType), isNullable);
        }

        private IEdmTypeReference MapStringProperties(KeyValuePair<string, JSchema> property, JSchema parent)
        {
            var value = property.Value;

            bool isNullable = !parent.Required.Contains(property.Key);
            bool isUnbounded = !property.Value.MaximumLength.HasValue;

            var stringReference = new EdmStringTypeReference(
                EdmCoreModel.Instance.GetPrimitiveType(EdmPrimitiveTypeKind.String),
                isNullable,
                isUnbounded,
                value.MaximumLength,
                false,
                true,
                property.Key);

            return stringReference;
        }

        public EdmPrimitiveTypeKind ToEdmPrimitiveType(JSchemaType type)
        {
            switch (type)
            {
                case JSchemaType.String:
                    return EdmPrimitiveTypeKind.String;
                case JSchemaType.Number:
                    return EdmPrimitiveTypeKind.Double;
                case JSchemaType.Integer:
                    return EdmPrimitiveTypeKind.Int32;
                case JSchemaType.Boolean:
                    return EdmPrimitiveTypeKind.Boolean;
                case JSchemaType.None:
                case JSchemaType.Object:
                case JSchemaType.Array:
                case JSchemaType.Null:
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}