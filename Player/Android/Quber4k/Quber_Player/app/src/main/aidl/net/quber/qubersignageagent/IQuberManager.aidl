package net.quber.qubersignageagent;

import net.quber.qubersignageagent.IQuberCallback;

interface IQuberManager {
    boolean sendRequestCmd(String jsonMsg);
    oneway void agentResponse(IQuberCallback responseCallback);
}
