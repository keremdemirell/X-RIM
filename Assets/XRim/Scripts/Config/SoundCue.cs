namespace XRim.Config
{
    /// <summary>
    /// Every sound the duel plays (GDD §2, Decided: metal clashes, impacts, joint snaps, bolt scatter, electric wall zaps, and comic
    /// effort grunts on attacks and impacts). The <c>Grunt…</c> cues are voices, played from a dummy's grunt pack (a cosmetic, §16); the
    /// rest come from an effects set. A cue's file name in <c>Assets/XRim/Audio/Incoming/</c> is its name in snake case
    /// (<c>AudioFileNaming</c>).
    /// </summary>
    public enum SoundCue
    {
        HitLight = 0,
        HitHeavy = 1,
        HitHead = 2,
        ClashRebound = 3,
        ClashCrush = 4,
        ClashSlide = 5,
        BlockFull = 6,
        BlockPartial = 7,
        JointSnap = 8,
        BoltScatter = 9,
        TorsoSplit = 10,
        WeaponDrop = 11,
        WallWarning = 12,
        WallZap = 13,
        SwingWhoosh = 14,
        BodyMove = 15,
        GruntAttack = 16,
        GruntEffort = 17,
        GruntHurt = 18,
        GruntBigHurt = 19,
        GruntKnockOut = 20,
    }
}
