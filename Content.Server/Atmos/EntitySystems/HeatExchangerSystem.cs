using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.Atmos.EntitySystems;

public sealed class HeatExchangerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private float _tileLoss;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeatExchangerComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);

        // Getting CVars is expensive, don't do it every tick
        Subs.CVar(_cfg, CCVars.SuperconductionTileLoss, CacheTileLoss, true);
    }

    private void CacheTileLoss(float val)
    {
        _tileLoss = val;
    }

    private void OnAtmosUpdate(EntityUid uid, HeatExchangerComponent comp, ref AtmosDeviceUpdateEvent args)
    {
        // make sure that the tile the device is on isn't blocked by a wall or something similar.
        if (args.Grid is { } grid
            && _transform.TryGetGridTilePosition(uid, out var tile)
            && _atmosphereSystem.IsTileAirBlocked(grid, tile))
        {
            return;
        }

        if (!_nodeContainer.TryGetNodes(uid, comp.InletName, comp.OutletName, out PipeNode? inlet, out PipeNode? outlet))
            return;

        var dt = args.Dt;

        // Goobstation
        var isInline = inlet == outlet;
        var fromInlet = true;

        // Goobstation - moved here from below
        GasMixture xfer;

        if (!isInline) // Goobstation
        {

            // Let n = moles(inlet) - moles(outlet), really a Δn
            var p = inlet.Air.Pressure - outlet.Air.Pressure; // really a ΔP
            // Such that positive P causes flow from the inlet to the outlet.

            // We want moles transferred to be proportional to the pressure difference, i.e.
            // dn/dt = G*P

            // To solve this we need to write dn in terms of P. Since PV=nRT, dP/dn=RT/V.
            // This assumes that the temperature change from transferring dn moles is negligible.
            // Since we have P=Pi-Po, then dP/dn = dPi/dn-dPo/dn = R(Ti/Vi - To/Vo):
            float dPdn = Atmospherics.R * (outlet.Air.Temperature / outlet.Air.Volume + inlet.Air.Temperature / inlet.Air.Volume);

            // Multiplying both sides of the differential equation by dP/dn:
            // dn/dt * dP/dn = dP/dt = G*P * (dP/dn)
            // Which is a first-order linear differential equation with constant (heh...) coefficients:
            // dP/dt + kP = 0, where k = -G*(dP/dn).
            // This differential equation has a closed-form solution, namely:
            float pFinal = p * MathF.Exp(-comp.G * dPdn * dt);

            // Finally, back out n, the moles transferred in this tick:
            float n = (p - pFinal) / dPdn;
            fromInlet = n > 0; // Goobstation

            if (fromInlet) // Goobstation
                xfer = inlet.Air.Remove(n);
            else
                xfer = outlet.Air.Remove(-n);
        }
        // Goobstation
        else
        {
            // if we're inline just operate on the pipenet's air directly
            xfer = inlet.Air;
        }

        float cXfer = _atmosphereSystem.GetHeatCapacity(xfer, true);
        if (cXfer < Atmospherics.MinimumHeatCapacity)
            return;

        var radTemp = Atmospherics.TCMB;

        var environment = _atmosphereSystem.GetContainingMixture(uid, true, true);
        bool hasEnv = false;
        float cEnv = 0f;
        if (environment != null)
        {
            cEnv = _atmosphereSystem.GetHeatCapacity(environment, true);
            hasEnv = cEnv >= Atmospherics.MinimumHeatCapacity && environment.TotalMoles > 0f;
            if (hasEnv)
                radTemp = environment.Temperature;
        }

        // How ΔT' scales in respect to heat transferred
        float tDivQ = 1f / cXfer;
        // Since it's ΔT, also account for the environment's temperature change
        if (hasEnv)
            tDivQ += 1f / cEnv;

        // Radiation
        float dTR = xfer.Temperature - radTemp;
        float dTRA = MathF.Abs(dTR);
        float a0 = _tileLoss / MathF.Pow(Atmospherics.T20C, 4);
        // ΔT' = -kΔT^4, k = -ΔT'/ΔT^4
        float kR = comp.Alpha * a0 * tDivQ;
        // Based on the fact that ((3t)^(-1/3))' = -(3t)^(-4/3) = -((3t)^(-1/3))^4, and ΔT' = -kΔT^4.
        float dT2R = dTR * MathF.Pow((1f + 3f * kR * dt * dTRA * dTRA * dTRA), -1f / 3f);
        float dER = (dTR - dT2R) / tDivQ;
        _atmosphereSystem.AddHeat(xfer, -dER);
        if (hasEnv && environment != null)
        {
            _atmosphereSystem.AddHeat(environment, dER);

            // Convection

            // Positive dT is from pipe to surroundings
            float dT = xfer.Temperature - environment.Temperature;
            // ΔT' = -kΔT, k = -ΔT' / ΔT
            float k = comp.K * tDivQ;
            float dT2 = dT * MathF.Exp(-k * dt);
            float dE = (dT - dT2) / tDivQ;
            _atmosphereSystem.AddHeat(xfer, -dE);
            _atmosphereSystem.AddHeat(environment, dE);
        }

        // Goobstation - if we're inline we're directly operating on the pipe's air so no need to re-merge
        if (!isInline)
        {
            if (fromInlet) // Goobstation
                _atmosphereSystem.Merge(outlet.Air, xfer);
            else
                _atmosphereSystem.Merge(inlet.Air, xfer);
        }

    }
}
