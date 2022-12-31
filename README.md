
# Valorant API C\# Library

This library allows you to make custom requests to Valorant Client API endpoints.








## Usage/Examples

Get PUUID from local endpoint
```
using ValorantAPI;
using Newtonsoft.Json;
internal class Program{
    static ValorantClient client = new ValorantClient(ValorantClient.REGION.eu);
    private string PUUID;
    static void Main(string[] args) {
        if(client.isConnected)
            getPUUID();
    }

    static async void getPUUID(){
        var data = Newtonsoft.Json.Linq.JObject.Parse(await client.Request(ValorantClient.REQUEST.GET, 
                        "/chat/v1/session", ValorantClient.ENDPOINT.E_LOCAL));
        PUUID = data["puuid"];
    }
}

```

Get data from GLZ endpoint
```
static async void getData(){
    var data = Newtonsoft.Json.Linq.JObject.Parse(await client.Request(ValorantClient.REQUEST.GET, 
                $"/parties/v1/players/{PUUID}", ValorantClient.ENDPOINT.E_GLZ))
}
```

For more examples go to [PoniLCU](https://github.com/Ponita0/PoniLCU)


## Credits

- [Ponita0](https://github.com/Ponita0) - a lot of the code comes from his PoniLCU library.
- [techchrism](https://github.com/techchrism) - his [Valorant API Docs](https://techchrism.github.io/valorant-api-docs/) helped a lot. 

