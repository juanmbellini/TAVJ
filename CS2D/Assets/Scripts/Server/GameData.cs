using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData {
    public float Time;
    public List<PlayerData> PlayersData;

    public GameData() {
        PlayersData = new List<PlayerData>();
    }


    public void Save(BitBuffer bitBuffer) {
        bitBuffer.PutFloat(Time);
        bitBuffer.PutInt(PlayersData.Count);
        foreach (var playerData in PlayersData) {
            playerData.Save(bitBuffer);
        }
    }

    public void Load(BitBuffer bitBuffer) {
        Time = bitBuffer.GetFloat();
        var playerCount = bitBuffer.GetInt();
        for (var i = 0; i < playerCount; i++) {
            var playerData = new PlayerData();
            playerData.Load(bitBuffer);
            PlayersData.Add(playerData);
        }
    }

    public List<PlayerData> Players {
        get { return PlayersData; }
    }
}