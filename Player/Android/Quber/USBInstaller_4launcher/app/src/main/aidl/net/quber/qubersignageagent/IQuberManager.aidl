package net.quber.qubersignageagent;

import net.quber.qubersignageagent.IQuberCallback;

interface IQuberManager {
    boolean sendRequestCmd(String jsonMsg);
    oneway void agentResponse(IQuberCallback responseCallback);
    boolean multiSendRequestCmd(String packageName, String jsonMsg);
    boolean multiAgentResponse(String packageName, IQuberCallback responseCallback);
    boolean multiClose(String packageName);
}
