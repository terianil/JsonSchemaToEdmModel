using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Edm;
using Microsoft.Data.Edm.Library;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NUnit.Framework;

namespace JsonSchemaToEdmModel.Tests
{
    [TestFixture]
    public class JsonSchemaConverterTest
    {
        private JsonSchemaConverter converter;

        [SetUp]
        public void SetUp()
        {
            this.converter = new JsonSchemaConverter();
        }

        [Test]
        public void JsonSchemaBasicTest()
        {
            JSchema schema = JSchema.Parse(@"{
              'type': 'object',
              'properties': {
                'name': {'type':'string'},
                'roles': {'type': 'array'}
              }
            }");

            JObject user = JObject.Parse(@"{
              'name': 'Arnie Admin',
              'roles': ['Developer', 'Administrator']
            }");

            Assert.IsTrue(user.IsValid(schema));
        }

        [Test]
        [TestCase(@"{'prop': {'type': 'string'}}", "prop", EdmPrimitiveTypeKind.String)]
        [TestCase(@"{'prop': {'type': 'integer'}}", "prop", EdmPrimitiveTypeKind.Int32)]
        [TestCase(@"{'prop': {'type': 'number'}}", "prop", EdmPrimitiveTypeKind.Double)]
        [TestCase(@"{'prop': {'type': 'boolean'}}", "prop", EdmPrimitiveTypeKind.Boolean)]
        [TestCase(@"{'prop': {'type': 'int'}}", "prop", EdmPrimitiveTypeKind.Boolean, ExpectedException = typeof(JSchemaReaderException))]
        public void JSchemaPrimitivesSchouldBeConvertedCorrectly(string schema, string name, EdmPrimitiveTypeKind type)
        {
            var json = @"{
              'type': 'object',
              'properties': __PROPERTY__
            }";


            var parsedSchema = JSchema.Parse(json.Replace("__PROPERTY__", schema));

            var model = converter.ToEdmModel(parsedSchema);

            var container = model.FindDeclaredEntityContainer("containerName");
            var entitySet = container.Elements.First() as EdmEntitySet;
            var property = entitySet.ElementType.StructuralProperties().First();

            Assert.AreEqual(name, property.Name);
            Assert.AreEqual(type, property.Type.AsPrimitive().PrimitiveKind());
        }

        [Test]
        public void JSchemaStringsWithRestrictionsShouldBeConvertedCorrectly()
        {
            var json =
            @"{
              'type': 'object',
              'properties': 
                {
                    'string': 
                    {
                        'type': 'string',
                        'minLength': 2,
                        'maxLength': 3,
                    }
                },
                'required': [ 'string' ]
            }";

            var parsedSchema = JSchema.Parse(json);

            var model = converter.ToEdmModel(parsedSchema);

            var container = model.FindDeclaredEntityContainer("containerName");
            var entitySet = container.Elements.First() as EdmEntitySet;
            var property = entitySet.ElementType.StructuralProperties().First();

            Assert.AreEqual("string", property.Name);
            Assert.AreEqual(EdmPrimitiveTypeKind.String, property.Type.AsPrimitive().PrimitiveKind());
            Assert.AreEqual(3, property.Type.AsString().MaxLength);
            Assert.AreEqual(false, property.Type.AsString().IsNullable);
        }

        [Test]
        public void JSchemaArraysShouldBeConvertedCorrectly()
        {
            var json =
            @"{
              'type': 'object',
              'properties': 
                {
                    'array': 
                    {
                        'type': 'array',
                        'items': {
                            'type': 'number'
                        }
                    }
                }
            }";

            var parsedSchema = JSchema.Parse(json);

            var model = converter.ToEdmModel(parsedSchema);

            var container = model.FindDeclaredEntityContainer("containerName");
            var entitySet = container.Elements.First() as EdmEntitySet;
            var property = entitySet.ElementType.StructuralProperties().First();

            Assert.AreEqual("array", property.Name);
            Assert.AreEqual(EdmPrimitiveTypeKind.Double, property.Type.AsCollection().CollectionDefinition().ElementType.PrimitiveKind());
        }

        [Test]
        public void JSchemaNestedObjectsShouldBeConvertedCorrectly()
        {
            var json =
            @"{
              'type': 'object',
              'properties': {
                'string': {
                  'type': 'string',
                  'minLength': 2,
                  'maxLength': 3,
                },
                'object': {
                  'type': 'object',
                  'properties': {
                    'string': {
                      'type': 'string',
                      'maxLength': 5,
                    },
                    'int': {
                      'type': 'integer'
                    }
                  }
                }
              },
              'required': [ 'string' ]
            }";

            var parsedSchema = JSchema.Parse(json);

            var model = converter.ToEdmModel(parsedSchema);

            var container = model.FindDeclaredEntityContainer("containerName");
            var entitySet = container.Elements.Single(x => x.Name == "root") as EdmEntitySet;
            var stringProperty = entitySet.ElementType.StructuralProperties().Single(x => x.Name == "string");
            var nestedObjectProperty = entitySet.ElementType.StructuralProperties().Single(x => x.Name == "object");
            var nestedObjectStructuralProperties =
                nestedObjectProperty.Type.AsEntityReference()
                    .EntityReferenceDefinition()
                    .EntityType.StructuralProperties()
                    .ToList();
            var nestedObjectPropertyString = nestedObjectStructuralProperties.Single(x => x.Name == "string");
            var nestedObjectPropertyInteger = nestedObjectStructuralProperties.Single(x => x.Name == "int");

            Assert.AreEqual("string", stringProperty.Name);
            Assert.AreEqual(EdmPrimitiveTypeKind.String, stringProperty.Type.AsPrimitive().PrimitiveKind());
            Assert.AreEqual(5, nestedObjectPropertyString.Type.AsString().MaxLength);
            Assert.AreEqual(false, stringProperty.Type.AsString().IsNullable);

            //nested object
            Assert.AreEqual("string", nestedObjectPropertyString.Name);
            Assert.AreEqual(EdmPrimitiveTypeKind.String, nestedObjectPropertyString.Type.AsPrimitive().PrimitiveKind());
            Assert.AreEqual(5, nestedObjectPropertyString.Type.AsString().MaxLength);
            Assert.AreEqual(true, nestedObjectPropertyString.Type.AsString().IsNullable);

            Assert.AreEqual("int", nestedObjectPropertyInteger.Name);
            Assert.AreEqual(EdmPrimitiveTypeKind.Int32, nestedObjectPropertyInteger.Type.AsPrimitive().PrimitiveKind());
        }

        [Test]
        public void JSchemaSchouldWorksCorrectly()
        {
            var schema = JSchema.Parse(@"{
              'type': 'object',
              'properties': {
                'name': {'type':'string'},
                'id': {'type': 'integer'},
                'object': {'type': 'object'}
              }
            }");
        }
    }
}
