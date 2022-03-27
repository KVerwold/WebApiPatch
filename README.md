# WebApiPatch - Dotnet Core Web Api with Patch RFC 7396 support.

The HTTP PATCH request method applies partial modifications to a resource.

PATCH must not be confused with POST or OUT, which have a complete different behavior when creating (POST) or complete replacing (PUT) a resource. For POST an PUT a complete presentation of a resource must be presented to the API.


From [RFC 6902](https://datatracker.ietf.org/doc/html/rfc6902) a PATCH request is considered a set of instructions on how to modify a resource. 
Those instructions are meta data carrying the operations to be performed to a specific path (property or embedded resource property) and controlling the functions
* add
* remove
* replace
* move 
* test


Here a sample:

```
[
     { "op": "test", "path": "/a/b/c", "value": "foo" },
     { "op": "remove", "path": "/a/b/c" },
     { "op": "add", "path": "/a/b/c", "value": [ "foo", "bar" ] },
     { "op": "replace", "path": "/a/b/c", "value": 42 },
     { "op": "move", "from": "/a/b/c", "path": "/a/b/d" },
     { "op": "copy", "from": "/a/b/d", "path": "/a/b/e" }
]
```
Hence this cumbersome protocol of controlling the update process by using those additional operators, a more lightweight approach is defined in standard [RFC 7396](https://datatracker.ietf.org/doc/html/rfc7396) by only using the presenting data as PATCH document and defining implicit processing rules to update the resources.

# PATCH document example using RFC 7396

(The following example is borrowed from rfc 7496 site)

Given the following example JSON document:


```
{
  "title": "Goodbye!",
  "phoneNumber": null,
  "author" : {
    "givenName" : "John",
    "familyName" : "Doe"
  },
  "tags":[ "example", "sample" ],
  "content": "This will be unchanged"
}
```


Applying a PATCH document as

```
{
  "title": "Hello!",
  "phoneNumber": "+01-123-456-7890",
  "author": {
    "familyName": null
  },
  "tags": [ "example" ]
}
```

The result would be

```
{
  "title": "Hello!",
  "author" : {
    "givenName" : "John",
    "familyName": null
  },
  "tags": [ "example" ],
  "content": "This will be unchanged",
  "phoneNumber": "+01-123-456-7890"
}
```
by carried out the following operations
* property "/title" has changed from "Goodbye!" to "Hello!"
* property "/phoneNumber" has be changed from null to "+01-123-456-7890" _(*)_
* property "/author/familyName" has changed from "Doe" to null _(*)_
* property "/tags" arrays has changed from ["example", "sample"] to ["example"] 

_(*) Changes made to original example hence .NET does not support dynamically adding/removing properties from classes which can be seen as an advantage or limitation :)_
